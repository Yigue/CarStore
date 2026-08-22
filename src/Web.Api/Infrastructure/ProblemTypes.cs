namespace Web.Api.Infrastructure;

/// <summary>
/// Canonical RFC 7231 <c>ProblemDetails.Type</c> URIs. Both <see cref="CustomResults"/>
/// (typed <c>Result</c> failures) and <see cref="GlobalExceptionHandler"/> (unhandled/framework
/// exceptions) are producers of <c>ProblemDetails</c> for the same status codes — they MUST
/// reference these constants instead of inlining the literal, so the two producers cannot drift
/// into different URIs for the same status (qa-p1-integridad, design D1).
/// </summary>
public static class ProblemTypes
{
    public const string BadRequest = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
    public const string Forbidden = "https://tools.ietf.org/html/rfc7231#section-6.5.3";
    public const string NotFound = "https://tools.ietf.org/html/rfc7231#section-6.5.4";
    public const string Conflict = "https://tools.ietf.org/html/rfc7231#section-6.5.8";
    public const string ServerError = "https://tools.ietf.org/html/rfc7231#section-6.6.1";
}
