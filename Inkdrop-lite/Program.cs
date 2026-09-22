using Inkdrop_lite.Authorization;
using Inkdrop_lite.Data;
using Inkdrop_lite.Features.Notebooks;
using Inkdrop_lite.Domain.Common;
using Inkdrop_lite.Features.Common;
using Inkdrop_lite.Features.Attachments;
using Inkdrop_lite.Features.Mcp;
using Inkdrop_lite.Features.Mcp.OAuth;
using Inkdrop_lite.Features.Notes;
using Inkdrop_lite.Features.Tags;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Identity.Web;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("Inkdrop-lite")
    ?? "Data Source=Inkdrop-lite.db";

var requiredScopes = builder.Configuration["AzureAd:Scopes"]?
    .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? [];

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? [];

if (requiredScopes.Length == 0)
{
    throw new InvalidOperationException("At least one AzureAd:Scopes value must be configured.");
}

// Database
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

// Application services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddScoped<INoteService, NoteService>();
builder.Services.AddScoped<INotebookService, NotebookService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddSingleton(new AttachmentStorageOptions
{
    Root = builder.Configuration["Attachments:Root"]
        ?? Path.Combine(builder.Environment.ContentRootPath, "data"),
    MaxBytes = builder.Configuration.GetValue<long?>("Attachments:MaxBytes")
        ?? AttachmentStorageOptions.DefaultMaxBytes
});
builder.Services.AddScoped<IAttachmentService, AttachmentService>();

// Authentication
// Tokens are always validated as Entra JWTs. The MCP scheme only decides how a 401 is *challenged*:
// it advertises OAuth protected-resource metadata that points MCP clients at this app's OAuth facade
// (Features/Mcp/OAuth), which in turn signs the user in through Entra.
var authentication = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
});

authentication.AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

var entraInstance = builder.Configuration["AzureAd:Instance"] ?? "https://login.microsoftonline.com/";
var entraTenantId = builder.Configuration["AzureAd:TenantId"];
var apiAudience = builder.Configuration["AzureAd:Audience"]
    ?? $"api://{builder.Configuration["AzureAd:ClientId"]}";

// Optional dedicated scope for MCP tokens (least privilege). Unset = MCP shares the API scope.
var mcpScopes = builder.Configuration["AzureAd:McpScope"] is { Length: > 0 } mcpScope
    ? new[] { mcpScope }
    : requiredScopes;

if (mcpScopes != requiredScopes && mcpScopes.Intersect(requiredScopes, StringComparer.Ordinal).Any())
{
    throw new InvalidOperationException("AzureAd:McpScope must differ from the AzureAd:Scopes values.");
}

var entraAuthority = $"{entraInstance.TrimEnd('/')}/{entraTenantId}";
var mcpOAuth = new McpOAuthOptions
{
    ClientId = builder.Configuration["AzureAd:ClientId"]
        ?? throw new InvalidOperationException("AzureAd:ClientId must be configured."),
    AuthorizeEndpoint = $"{entraAuthority}/oauth2/v2.0/authorize",
    TokenEndpoint = $"{entraAuthority}/oauth2/v2.0/token",
    // The MCP client only ever asks for the MCP scope, so its token cannot be used against /api.
    Scopes = [.. mcpScopes.Select(scope => $"{apiAudience}/{scope}"), "offline_access"],
    PublicUrl = builder.Configuration["Mcp:PublicUrl"]
};

builder.Services.AddSingleton(mcpOAuth);
builder.Services.AddHttpClient(McpOAuthEndpoints.EntraHttpClient, client =>
    client.Timeout = TimeSpan.FromSeconds(30));

authentication.AddMcp(options =>
{
    options.Events.OnResourceMetadataRequest = context =>
    {
        var request = context.HttpContext.Request;

        context.ResourceMetadata = new ProtectedResourceMetadata
        {
            Resource = $"{mcpOAuth.BaseUrl(request)}/mcp",
            ResourceName = "Inkdrop-lite",
            AuthorizationServers = { mcpOAuth.BaseUrl(request) },
            ScopesSupported = [.. mcpOAuth.Scopes.Where(scope => scope != "offline_access")]
        };

        return Task.CompletedTask;
    };
});

// Authorization
builder.Services
    .AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.ApiScope, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => HasAnyScope(context.User, requiredScopes));
    })
    .AddPolicy(AuthorizationPolicies.McpScope, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => HasAnyScope(context.User, mcpScopes));
    })
    .SetFallbackPolicy(
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build());

// Controllers
builder.Services.AddControllers();

// MCP server (stateless: every request is authenticated and scoped on its own)
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<InkdropTools>()
    .WithRequestFilters(filters => filters.AddCallToolFilter(next => (context, cancellationToken) =>
    {
        // Label writes made through MCP with the calling client, e.g. "mcp:claude-code/1.4".
        var client = context.Server.ClientInfo;
        var source = string.IsNullOrWhiteSpace(client?.Name)
            ? ChangeSources.Mcp
            : $"{ChangeSources.Mcp}:{client.Name}" +
              (string.IsNullOrWhiteSpace(client.Version) ? "" : $"/{client.Version}");

        context.Services!.GetRequiredService<IHttpContextAccessor>().HttpContext!
            .Items[HttpContextCurrentUser.ChangeSourceItemKey] = source;

        return next(context, cancellationToken);
    }));

// OpenAPI
builder.Services.AddOpenApi();

// Problem Details
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<RuleViolationExceptionHandler>();

// Response compression
builder.Services.AddResponseCompression();

// Health checks
builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddDbContextCheck<AppDbContext>("sqlite", tags: ["ready"]);

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });

    // Anonymous OAuth endpoints get their own per-IP budget so an unauthenticated flood
    // cannot use up the shared "fixed" quota that signed-in users depend on.
    options.AddPolicy(McpOAuthEndpoints.RateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Logs
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console()
        .WriteTo.File(
            "logs/inkdrop-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseExceptionHandler();

app.UseResponseCompression();

app.UseSerilogRequestLogging();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
})
.AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
})
.AllowAnonymous();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers()
    .RequireRateLimiting("fixed");

app.MapMcpOAuth();

app.MapMcp("/mcp")
    .RequireAuthorization(AuthorizationPolicies.McpScope)
    .RequireRateLimiting("fixed");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await db.Database.MigrateAsync();
}

app.Run();

static bool HasAnyScope(ClaimsPrincipal user, string[] scopes) =>
    user.Claims
        .Where(claim => claim.Type is
            "scp" or "http://schemas.microsoft.com/identity/claims/scope")
        .SelectMany(claim => claim.Value.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Any(scope => scopes.Contains(scope, StringComparer.Ordinal));

public partial class Program;
