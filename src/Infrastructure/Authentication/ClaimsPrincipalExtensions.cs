using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Infrastructure.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal? principal)
    {
        // Depending on the JwtBearer claim mapping configuration, the user id
        // may arrive either as ClaimTypes.NameIdentifier (when inbound mapping
        // is on) or as the raw JwtRegisteredClaimNames.Sub claim. Try both so
        // changes in the auth pipeline don't silently break authorization.
        string? userId =
            principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(userId, out Guid parsedUserId) ?
            parsedUserId :
            throw new ApplicationException("User id is unavailable");
    }

    /// <summary>
    /// Checks whether the principal is a legitimate platform-level SuperAdmin.
    /// Requires platform_role == "super_admin" AND dealer_id to be empty or absent.
    /// If a token carries platform_role with a non-empty tenant dealer_id, it is NOT
    /// treated as a platform SuperAdmin (fail-closed against forged/leaked cross-tenant tokens).
    /// </summary>
    public static bool IsPlatformSuperAdmin(this ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return false;
        }

        var platformRole = principal.FindFirst("platform_role")?.Value;
        if (!string.Equals(platformRole, "super_admin", StringComparison.Ordinal))
        {
            return false;
        }

        var dealerIdClaim = principal.FindFirst("dealer_id")?.Value;
        if (dealerIdClaim is not null && Guid.TryParse(dealerIdClaim, out var dealerId) && dealerId != Guid.Empty)
        {
            return false;
        }

        return true;
    }
}
