using Application.Cars.Consignment;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

/// <summary>sfc B7.1 / RN-036: puts a car on consignment (owner + commission) or ends it (owner null).</summary>
internal sealed class SetConsignment : IEndpoint
{
    public sealed record Request(Guid? OwnerClientId, decimal? CommissionRate);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("cars/{id:guid}/consignment", async (Guid id, Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            Result result = await sender.Send(
                new SetConsignmentCommand(CarId: id, OwnerClientId: request.OwnerClientId, CommissionRate: request.CommissionRate),
                cancellationToken);
            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission("cars:update")
        .WithTags(Tags.Cars)
        .WithName("SetConsignment");
    }
}
