using Application.DealerSettings.Commands.SetBusinessHours;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.DealerSettings;

/// <summary>
/// sfc B5.2 / RN-044: lets the dealer configure the weekly schedule the public test-drive slot
/// query (<c>GET public/cars/{id}/test-drive-slots</c>) reads from. Not one of the explicitly
/// listed B5.2 sub-tasks, but required for the feature to be usable at all — without it
/// <c>DealerSettings.BusinessHours</c> is always empty and every slot query returns nothing.
/// </summary>
internal sealed class SetBusinessHours : IEndpoint
{
    public sealed record EntryRequest(DayOfWeek DayOfWeek, TimeOnly? OpenTime, TimeOnly? CloseTime);

    public sealed record Request(IReadOnlyList<EntryRequest>? Entries);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("dealer-settings/business-hours", async (
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new SetBusinessHoursCommand(
                request.Entries?
                    .Select(e => new BusinessHoursEntryDto(e.DayOfWeek, e.OpenTime, e.CloseTime))
                    .ToList());

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                _ => Results.Ok(),
                CustomResults.Problem);
        })
        .HasPermission("CanManageSettings")
        .WithTags(Tags.DealerSettings)
        .WithName("SetDealerBusinessHours")
        .Produces(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
