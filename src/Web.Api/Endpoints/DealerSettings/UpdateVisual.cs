using Application.DealerSettings.Commands.UpdateDealerVisual;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.DealerSettings;

internal sealed class UpdateVisual : IEndpoint
{
    public sealed record Request(
        string? LogoUrl,
        string? PrimaryColor,
        string? SecondaryColor,
        string? FooterText);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("dealer-settings/visual", async (
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateDealerVisualCommand(
                request.LogoUrl,
                request.PrimaryColor,
                request.SecondaryColor,
                request.FooterText);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                _ => Results.Ok(),
                CustomResults.Problem);
        })
        .HasPermission("CanManageSettings")
        .WithTags(Tags.DealerSettings)
        .WithName("UpdateDealerVisual")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}