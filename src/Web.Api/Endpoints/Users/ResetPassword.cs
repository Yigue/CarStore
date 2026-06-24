using Application.Users.ResetPassword;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

/// <summary>
/// Confirma el reseteo de contraseña: recibe el token enviado por email y la
/// nueva contraseña. Token inválido o expirado → 400 (Error.Problem).
/// </summary>
internal sealed class ResetPassword : IEndpoint
{
    public sealed record Request(string Token, string NewPassword);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/reset-password", async (
            [FromBody] Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new ResetPasswordCommand(request.Token, request.NewPassword);

            Result result = await sender.Send(command, cancellationToken);

            return result.Match(
                () => Results.Ok(new { message = "Contraseña actualizada correctamente." }),
                CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .WithName("ResetPassword")
        .AllowAnonymous()
        .RequireRateLimiting("login")
        .Produces(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
