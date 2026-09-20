using System.Security.Claims;
using Inkdrop_lite.Domain.Common;

namespace Inkdrop_lite.Authorization;

public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
    : ICurrentUser
{
    /// <summary>HttpContext.Items key an MCP request filter uses to label the acting client.</summary>
    public const string ChangeSourceItemKey = "inkdrop.change-source";

    public string ChangeSource =>
        httpContextAccessor.HttpContext?.Items[ChangeSourceItemKey] as string
        ?? ChangeSources.App;

    public string? UserId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;

            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var objectId = user.FindFirstValue("oid")
                ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(objectId))
            {
                return null;
            }

            var tenantId = user.FindFirstValue("tid");
            return string.IsNullOrWhiteSpace(tenantId)
                ? objectId
                : $"{tenantId}:{objectId}";
        }
    }
}
