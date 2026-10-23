using Application.Users.SelectDealer;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Auth;

/// <summary>sfc B6.3 / RN-043, scenario 2: redeems the pending-dealer-selection token Login issued for
/// a real session. Requires a valid JWT (the pending token IS one) but no specific permission — the
/// handler itself verifies the `login_stage` claim and the requested dealer's membership.</summary>
internal sealed class SelectDealer : IEndpoint
{
    public sealed record Request(Guid DealerId);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("auth/select-dealer", async (
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new SelectDealerCommand(request.DealerId);

            Result<string> result = await sender.Send(command, cancellationToken);

            return result.Match(
                token => Results.Ok(new { token }),
                CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Auth)
        .WithName("SelectDealer")
        .Produces<object>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
