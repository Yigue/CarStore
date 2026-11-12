using System;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Web.Api.Infrastructure;

/// <summary>
/// Marker contract for a minimal-API request record that carries a CAPTCHA token.
/// spr PR 3.3 (B10).
/// </summary>
public interface IHasCaptchaToken
{
    string? CaptchaToken { get; }
}

/// <summary>
/// Endpoint filter that blocks a request BEFORE it reaches the MediatR handler unless it carries a
/// valid CAPTCHA token. Runs after the ASP.NET Core rate limiter middleware (that is applied via
/// <c>.RequireRateLimiting(...)</c> on the route, which wraps endpoint execution) and before any
/// FluentValidation/handler logic, so a missing or invalid token never creates any row.
/// spr PR 3.3 (B10): fail closed — <see cref="CaptchaVerificationResult.Unavailable"/> (provider
/// timeout/outage) returns 503, never lets the request through.
/// </summary>
public sealed class CaptchaEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        IHasCaptchaToken? request = context.Arguments.OfType<IHasCaptchaToken>().FirstOrDefault();
        string? token = request?.CaptchaToken;

        if (string.IsNullOrWhiteSpace(token))
        {
            return Results.Problem(
                title: "captcha.missing_token",
                detail: "A CAPTCHA token is required.",
                type: ProblemTypes.BadRequest,
                statusCode: StatusCodes.Status400BadRequest);
        }

        var verifier = context.HttpContext.RequestServices.GetRequiredService<ICaptchaVerifier>();
        CaptchaVerificationResult result = await verifier.VerifyAsync(token, context.HttpContext.RequestAborted);

        return result switch
        {
            CaptchaVerificationResult.Valid => await next(context),
            CaptchaVerificationResult.Invalid => Results.Problem(
                title: "captcha.invalid_token",
                detail: "The CAPTCHA token is invalid or expired.",
                type: ProblemTypes.BadRequest,
                statusCode: StatusCodes.Status400BadRequest),
            CaptchaVerificationResult.Unavailable => Results.Problem(
                title: "captcha.provider_unavailable",
                detail: "The CAPTCHA provider could not be reached. Please try again.",
                type: ProblemTypes.ServerError,
                statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => throw new InvalidOperationException($"Unhandled {nameof(CaptchaVerificationResult)}: {result}")
        };
    }
}
