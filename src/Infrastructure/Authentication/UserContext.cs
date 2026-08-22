using Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Authentication;

internal sealed class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId =>
        _httpContextAccessor
            .HttpContext?
            .User
            .GetUserId() ??
        throw new ApplicationException("User context is unavailable");

    /// <summary>
    /// Reads the <c>role</c> claim that TokenProvider writes. Case-insensitive
    /// because the claim's casing has drifted before (the JWT carries "Admin",
    /// some older tokens lowercase it). No claim means not an admin — the caller
    /// gets the safe answer rather than an exception, since this only ever hides
    /// a field.
    /// </summary>
    public bool IsAdmin =>
        string.Equals(
            _httpContextAccessor.HttpContext?.User?.FindFirst("role")?.Value,
            "Admin",
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether authentication actually succeeded for this request. On an
    /// <c>AllowAnonymous</c> endpoint the pipeline still populates
    /// <c>HttpContext.User</c> with an unauthenticated principal, so the
    /// presence of <c>User</c> proves nothing — <c>Identity.IsAuthenticated</c>
    /// is what distinguishes a verified caller from a visitor.
    /// </summary>
    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
}
