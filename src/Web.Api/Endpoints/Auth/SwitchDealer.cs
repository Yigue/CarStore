using Application.Users.SwitchDealer;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Auth;

/// <summary>sfc B6.3 / RN-043, scenario 3: re-scopes an already authenticated session to another of
/// the caller's dealers, without logging out. Requires a normal full session JWT, no specific
/// permission — a pending-dealer-selection token is rejected by the handler.</summary>
internal sealed class SwitchDealer : IEndpoint
{
    public sealed record Request(Guid DealerId);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("auth/switch-dealer", async (
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new SwitchDealerCommand(request.DealerId);

            Result<string> result = await sender.Send(command, cancellationToken);

            return result.Match(
                token => Results.Ok(new { token }),
                CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Auth)
        .WithName("SwitchDealer")
        .Produces<object>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
