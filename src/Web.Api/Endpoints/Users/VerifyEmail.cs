using Application.Users.Verification.VerifyEmail;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

/// <summary>
/// sfc A4.1 / RN-030: confirms the email with the single-use token sent by mail. Unknown, expired, used,
/// revoked or foreign tokens all answer 400 with the same body.
/// </summary>
internal sealed class VerifyEmail : IEndpoint
{
    public sealed record Request(string Token);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/verify-email", async (
            [FromBody] Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            Result result = await sender.Send(new VerifyEmailCommand(request.Token), cancellationToken);

            return result.Match(
                () => Results.Ok(new { message = "Email verificado correctamente." }),
                CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .WithName("VerifyEmail")
        .AllowAnonymous()
        .RequireRateLimiting("login")
        .Produces(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
