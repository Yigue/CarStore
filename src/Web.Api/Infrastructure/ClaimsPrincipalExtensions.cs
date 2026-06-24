using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Web.Api.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Gets the DealerId from the JWT "dealer_id" claim.
    /// </summary>
    public static Guid? GetDealerId(this ClaimsPrincipal? principal)
    {
        string? dealerIdClaim = principal?.FindFirstValue("dealer_id");

        return Guid.TryParse(dealerIdClaim, out Guid dealerId) ? dealerId : null;
    }

    /// <summary>
    /// Checks if the user has the Admin role.
    /// </summary>
    public static bool IsAdmin(this ClaimsPrincipal? principal)
    {
        // Check for Admin role claim (roles come from JWT as "role" claim)
        var roleClaim = principal?.FindFirstValue("role");

        return string.Equals(roleClaim, "Admin", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the user has a specific permission.
    /// Permissions are stored as individual claims with type "permission".
    /// </summary>
    public static bool HasPermission(this ClaimsPrincipal? principal, string permission)
    {
        if (principal is null)
        {
            return false;
        }

        // Check for permission claim - permissions are stored as separate claims
        return principal.Claims.Any(c =>
            string.Equals(c.Type, "permission", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase));
    }
}