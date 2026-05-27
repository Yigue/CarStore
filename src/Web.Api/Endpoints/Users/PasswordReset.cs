using Microsoft.AspNetCore.Mvc;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

/// <summary>
/// Endpoint stub para solicitar el reset de contraseña.
///
/// Actualmente acepta el request y responde 200 OK con un mensaje neutral
/// (no revela si el email existe — evita enumeración de cuentas).
///
/// TODO: integrar con SMTP/SendGrid + tabla PasswordResetTokens para enviar
/// el mail con el link de reseteo. Tracking en deuda-tecnica-backend.
/// </summary>
internal sealed class PasswordReset : IEndpoint
{
    public sealed record Request(string Email);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/password-reset", (
            [FromBody] Request request,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PasswordReset");
            logger.LogInformation(
                "Password reset solicitado para {Email}. STUB: aún no se envía email — pendiente integración SMTP.",
                request.Email);

            // Respuesta neutral por seguridad: no confirmamos si el email existe.
            return Results.Ok(new
            {
                message = "Si la cuenta existe, recibirás instrucciones para restablecer tu contraseña.",
                stub = true
            });
        })
        .WithTags(Tags.Users)
        .WithName("RequestPasswordReset")
        .AllowAnonymous()
        .RequireRateLimiting("login")
        .Produces(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
