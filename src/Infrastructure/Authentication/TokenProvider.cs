using System.Security.Claims;
using System.Text;
using Application.Abstractions.Authentication;
using Domain.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Authentication;

internal sealed class TokenProvider(IConfiguration configuration) : ITokenProvider
{
    /// <summary>sfc B6.3 / RN-043: non-standard claim marking a pending-dealer-selection token. Read back by
    /// <see cref="UserContext.IsPendingDealerSelection"/>.</summary>
    public const string LoginStageClaimType = "login_stage";
    public const string PendingDealerSelectionStage = "pending_dealer_selection";

    /// <summary>Default lifetime for a pending-dealer-selection token when <c>Jwt:PendingSelectionExpirationInMinutes</c>
    /// is not configured — short on purpose: it only needs to survive the dealer-picker screen.</summary>
    private const int DefaultPendingSelectionExpirationMinutes = 5;

    public string Create(User user, Guid dealerId, string roleName)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("dealer_id", dealerId.ToString()),
            new Claim("role", roleName)
        };

        // ADR-1: emit platform_role claim only for SuperAdmin users.
        // Claim absence is the correct signal for all tenant-scoped roles.
        // SuperAdmin status is identified strictly by user.IsPlatformAdmin (DealerId == Guid.Empty).
        if (user.IsPlatformAdmin)
        {
            claims.Add(new Claim("platform_role", "super_admin"));
        }

        int expirationMinutes = configuration.GetValue<int>("Jwt:ExpirationInMinutes");

        return CreateToken(claims, expirationMinutes);
    }

    public string CreatePendingDealerSelection(User user)
    {
        // sfc B6.3 / RN-043: deliberately NO dealer_id, NO role claim — this token authenticates the
        // caller (sub) only enough to call POST auth/select-dealer; every tenant-scoped query still fails
        // to resolve a tenant for it.
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(LoginStageClaimType, PendingDealerSelectionStage)
        };

        int expirationMinutes = configuration.GetValue(
            "Jwt:PendingSelectionExpirationInMinutes", DefaultPendingSelectionExpirationMinutes);

        return CreateToken(claims, expirationMinutes);
    }

    private string CreateToken(List<Claim> claims, int expirationMinutes)
    {
        string secretKey = configuration["Jwt:Secret"]!;
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
            SigningCredentials = credentials,
            Issuer = configuration["Jwt:Issuer"],
            Audience = configuration["Jwt:Audience"]
        };

        var handler = new JsonWebTokenHandler();

        return handler.CreateToken(tokenDescriptor);
    }
}
