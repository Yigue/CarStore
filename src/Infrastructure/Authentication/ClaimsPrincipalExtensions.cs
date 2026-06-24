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
}
