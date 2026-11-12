using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Legal;
using Infrastructure.Caching;

namespace Web.Api.Infrastructure;

/// <summary>
/// RN-016 acceptance gate. Write requests (anything but GET/HEAD/OPTIONS) from an authenticated user who has
/// not accepted the current <em>material</em> Terms version are rejected with 428
/// <c>{ error: "legal.acceptance_required", version }</c>. Reads are never blocked.
/// The accepted version comes from the JWT claim <c>tv</c>, with <see cref="ICacheService"/> as fallback so a
/// user who just accepted is not blocked until the token is refreshed. Runs after authentication and before
/// the subscription guard; enabled by <c>FeatureFlags:LegalGate</c>.
/// </summary>
public sealed class LegalAcceptanceGateMiddleware(RequestDelegate next)
{
    public const string TermsVersionClaim = "tv";
    public const string ErrorCode = "legal.acceptance_required";

    /// <summary>Cache entry (string version) the acceptance endpoint writes after recording an acceptance.</summary>
    public static string CacheKey(Guid userId) => $"legal:terms-version:{userId}";

    // POST legal/acceptances (how the user re-accepts), logout and users/me must stay reachable behind the gate.
    private static readonly Regex[] ExemptPaths =
    [
        new(@"^/api/v\d+/legal/acceptances/?$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^/api/v\d+/users/(logout|me)/?$", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method)
            || HttpMethods.IsHead(context.Request.Method)
            || HttpMethods.IsOptions(context.Request.Method)
            || context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        string path = context.Request.Path.Value ?? string.Empty;
        foreach (Regex exempt in ExemptPaths)
        {
            if (exempt.IsMatch(path))
            {
                await next(context);
                return;
            }
        }

        ILegalTermsProvider terms = context.RequestServices.GetRequiredService<ILegalTermsProvider>();
        LegalTermsInfo current = terms.Current;

        // A non-material change never blocks.
        if (!current.Material || string.IsNullOrEmpty(current.Version))
        {
            await next(context);
            return;
        }

        if (string.Equals(context.User.FindFirst(TermsVersionClaim)?.Value, current.Version, StringComparison.Ordinal)
            || await CachedVersionMatchesAsync(context, current.Version))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status428PreconditionRequired;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new { error = ErrorCode, version = current.Version }),
            context.RequestAborted);
    }

    private static async Task<bool> CachedVersionMatchesAsync(HttpContext context, string version)
    {
        string? sub = context.User.FindFirst("sub")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out Guid userId))
        {
            return false;
        }

        ICacheService cache = context.RequestServices.GetRequiredService<ICacheService>();
        string? cached = await cache.GetAsync<string>(CacheKey(userId), context.RequestAborted);
        return string.Equals(cached, version, StringComparison.Ordinal);
    }
}
