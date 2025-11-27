using System.Security.Claims;

namespace Siestur.Extensions;

/// <summary>
/// Extension methods for ClaimsPrincipal to simplify user identity extraction
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var userIdStr = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdStr, out var userId) ? userId : Guid.Empty;
    }

    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
    {
        var userIdStr = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdStr, out userId);
    }
}
