using Microsoft.AspNetCore.Mvc;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

/// <summary>
/// Endpoint stub para reenviar el email de verificación.
///
/// TODO: integrar con SMTP y la tabla EmailVerificationTokens.
/// </summary>
internal sealed class ResendVerification : IEndpoint
{
    public sealed record Request(string Email);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/resend-verification", (
            [FromBody] Request request,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("ResendVerification");
            logger.LogInformation(
                "Reenvío de verificación solicitado para {Email}. STUB: aún no se envía email.",
                request.Email);

            return Results.Ok(new
            {
                message = "Si la cuenta existe y no está verificada, te reenviaremos el email.",
                stub = true
            });
        })
        .WithTags(Tags.Users)
        .WithName("ResendVerification")
        .AllowAnonymous()
        .RequireRateLimiting("login")
        .Produces(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
