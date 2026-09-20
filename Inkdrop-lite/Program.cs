using Inkdrop_lite.Authorization;
using Inkdrop_lite.Data;
using Inkdrop_lite.Features.Notebooks;
using Inkdrop_lite.Features.Notes;
using Inkdrop_lite.Features.Tags;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Identity.Web;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("Inkdrop-lite")
    ?? "Data Source=Inkdrop-lite.db";

var requiredScopes = builder.Configuration["AzureAd:Scopes"]?
    .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? [];

if (requiredScopes.Length == 0)
{
    throw new InvalidOperationException("At least one AzureAd:Scopes value must be configured.");
}

// Database
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

// Application services
builder.Services.AddScoped<INoteService, NoteService>();
builder.Services.AddScoped<INotebookService, NotebookService>();
builder.Services.AddScoped<ITagService, TagService>();

// Authentication
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(
        builder.Configuration.GetSection("AzureAd"));

// Authorization
builder.Services
    .AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.ApiScope, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            context.User.Claims
                .Where(claim => claim.Type is
                    "scp" or "http://schemas.microsoft.com/identity/claims/scope")
                .SelectMany(claim => claim.Value.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Any(scope => requiredScopes.Contains(scope, StringComparer.Ordinal)));
    })
    .SetFallbackPolicy(
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build());

// Controllers
builder.Services.AddControllers();

// OpenAPI
builder.Services.AddOpenApi();

// Problem Details
builder.Services.AddProblemDetails();

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

    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "https://notes.your-domain.com")
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
    app.UseExceptionHandler();
    app.UseHsts();
}

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

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers()
    .RequireRateLimiting("fixed");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await db.Database.MigrateAsync();
}

app.Run();

public partial class Program;
