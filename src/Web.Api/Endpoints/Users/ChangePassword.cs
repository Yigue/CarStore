using Application.Users.ChangePassword;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

/// <summary>
/// Cambia la contraseña del usuario autenticado.
/// </summary>
/// <remarks>
/// `RequireAuthorization()` a secas, sin `.HasPermission(...)`: cualquier usuario
/// logueado puede cambiar SU propia contraseña, y exigir un permiso de gestión de
/// usuarios habría dejado afuera justamente a quien más lo necesita. El objetivo
/// sale de IUserContext, no de la petición, así que no hay a quién apuntar.
///
/// Comparte el rate limiting "login" con el resto de los endpoints de credenciales:
/// este acepta la contraseña actual, o sea que es adivinable por fuerza bruta igual
/// que el login.
/// </remarks>
internal sealed class ChangePassword : IEndpoint
{
    public sealed record Request(string CurrentPassword, string NewPassword);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/change-password", async (
            [FromBody] Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new ChangePasswordCommand(request.CurrentPassword, request.NewPassword);

            Result result = await sender.Send(command, cancellationToken);

            return result.Match(
                () => Results.Ok(new { message = "Contraseña actualizada correctamente." }),
                CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .WithName("ChangePassword")
        .RequireAuthorization()
        .RequireRateLimiting("login")
        .Produces(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
