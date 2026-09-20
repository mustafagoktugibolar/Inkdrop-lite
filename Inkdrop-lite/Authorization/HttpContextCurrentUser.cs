using System.Security.Claims;

namespace Inkdrop_lite.Authorization;

public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
    : ICurrentUser
{
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
