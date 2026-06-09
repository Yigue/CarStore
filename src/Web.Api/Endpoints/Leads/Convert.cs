using Application.Leads.Convert;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Leads;

internal sealed class Convert : IEndpoint
{
    public sealed record Request(string Dni, string Address);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("leads/{id:guid}/convert", async (Guid id, Request request, ISender sender, CancellationToken ct) =>
        {
            var command = new ConvertLeadToClientCommand(id, request.Dni, request.Address);
            Result<Guid> result = await sender.Send(command, ct);
            return result.Match(
                clientId => Results.Created($"/clients/{clientId}", new { clientId }),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.LeadsUpdate)
        .WithTags(Tags.Leads)
        .WithName("ConvertLeadToClient")
        .Produces<Guid>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
