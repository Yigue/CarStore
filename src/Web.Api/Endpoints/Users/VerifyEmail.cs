using Microsoft.AspNetCore.Mvc;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

/// <summary>
/// Endpoint stub para verificación de email.
///
/// TODO: integrar con tabla EmailVerificationTokens. El token se genera al
/// registrar al usuario, se manda por mail, y este endpoint lo valida y marca
/// User.IsEmailVerified = true (propiedad a agregar al domain).
/// </summary>
internal sealed class VerifyEmail : IEndpoint
{
    public sealed record Request(string Token);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/verify-email", (
            [FromBody] Request request,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("VerifyEmail");
            logger.LogInformation(
                "Verificación de email solicitada con token {Token}. STUB: validación todavía no implementada.",
                request.Token);

            return Results.Ok(new
            {
                message = "Email marcado como verificado (modo desarrollo).",
                stub = true
            });
        })
        .WithTags(Tags.Users)
        .WithName("VerifyEmail")
        .AllowAnonymous()
        .Produces(StatusCodes.Status200OK)
        .ProducesValidationProblem();
    }
}
