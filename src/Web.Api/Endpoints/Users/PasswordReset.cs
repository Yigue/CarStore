using Microsoft.AspNetCore.Mvc;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

/// <summary>
/// Solicita el reset de contraseña. Genera un token de un solo uso (válido 1h),
/// lo persiste en password_reset_tokens y envía por email el link
/// {Frontend:BaseUrl}/reset-password?token=... (ver RequestPasswordResetCommandHandler).
///
/// Responde 200 OK con un mensaje neutral (no revela si el email existe — evita
/// enumeración de cuentas).
/// </summary>
internal sealed class PasswordReset : IEndpoint
{
    public sealed record Request(string Email);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/forgot-password", async (
            [FromBody] Request request,
            MediatR.ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new Application.Users.Commands.RequestPasswordReset.RequestPasswordResetCommand(request.Email);
            await sender.Send(command, cancellationToken);

            // Respuesta neutral por seguridad: no confirmamos si el email existe.
            return Results.Ok(new
            {
                message = "Si la cuenta existe, recibirás instrucciones para restablecer tu contraseña."
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
