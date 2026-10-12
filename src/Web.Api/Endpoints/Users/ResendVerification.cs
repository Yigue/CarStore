using Application.Users.Verification.ResendVerification;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

/// <summary>
/// sfc A4.1 / RN-030: re-sends the verification email. The answer is identical whether the account exists,
/// is already verified, or the email could not be sent (no account enumeration).
/// </summary>
internal sealed class ResendVerification : IEndpoint
{
    public sealed record Request(string Email);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/resend-verification", async (
            [FromBody] Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new ResendVerificationCommand(request.Email), cancellationToken);

            return Results.Ok(new { message = "Si la cuenta existe y no está verificada, te reenviaremos el email." });
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
