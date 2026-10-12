using Application.Users.OwnerTransfer.AcceptOwnerTransfer;
using Application.Users.OwnerTransfer.InitiateOwnerTransfer;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

/// <summary>
/// sfc A4.3 / A22 / RN-029: owner transfer with double confirmation. <c>POST dealer/owner-transfer</c> (current owner only,
/// enforced by the handler against <c>DealerSettings.OwnerUserId</c>) emails a 48 h token to the recipient;
/// <c>POST dealer/owner-transfer/accept</c> (the recipient, authenticated) consumes it and swaps the owner.
/// </summary>
internal sealed class OwnerTransfer : IEndpoint
{
    public sealed record InitiateRequest(Guid TargetUserId);

    public sealed record AcceptRequest(string Token);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("dealer/owner-transfer", async (
                [FromBody] InitiateRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                Result result = await sender.Send(new InitiateOwnerTransferCommand(request.TargetUserId), cancellationToken);

                return result.Match(
                    () => Results.Ok(new { message = "Enviamos un email de confirmación al destinatario." }),
                    CustomResults.Problem);
            })
            .WithTags(Tags.Users)
            .WithName("InitiateOwnerTransfer")
            .RequireAuthorization()
            .RequireRateLimiting("login")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("dealer/owner-transfer/accept", async (
                [FromBody] AcceptRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                Result result = await sender.Send(new AcceptOwnerTransferCommand(request.Token), cancellationToken);

                return result.Match(
                    () => Results.Ok(new { message = "Ahora sos el titular de la cuenta." }),
                    CustomResults.Problem);
            })
            .WithTags(Tags.Users)
            .WithName("AcceptOwnerTransfer")
            .RequireAuthorization()
            .RequireRateLimiting("login")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
