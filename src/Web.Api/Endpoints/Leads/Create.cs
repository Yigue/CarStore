using Application.Leads.Create;
using Domain.Leads;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Leads;

internal sealed class Create : IEndpoint
{
    public sealed record Request(
        string ClientName,
        string Email,
        string Phone,
        LeadSource Source,
        string? Notes,
        Guid? InterestedVehicleId = null);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("leads", async (Request request, ISender sender, CancellationToken ct) =>
        {
            var command = new CreateLeadCommand(
                request.ClientName,
                request.Email,
                request.Phone,
                request.Source,
                request.Notes,
                request.InterestedVehicleId);

            Result<Guid> result = await sender.Send(command, ct);

            return result.Match(
                id => Results.Created($"/leads/{id}", new { id }),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.LeadsCreate)
        .WithTags(Tags.Leads)
        .WithName("CreateLead")
        .Produces<Guid>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
