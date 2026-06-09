using Application.Appointments.Commands.CreateAppointment;
using Domain.Appointments;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Appointments;

internal sealed class Create : IEndpoint
{
    public sealed record Request(
        Guid VehicleId,
        Guid? ClientId,
        Guid? LeadId,
        Guid AgentId,
        DateTime Start,
        DateTime End,
        AppointmentType Type,
        string? Notes);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("appointments", async (Request request, ISender sender, CancellationToken ct) =>
        {
            var command = new CreateAppointmentCommand(
                request.VehicleId,
                request.ClientId,
                request.LeadId,
                request.AgentId,
                request.Start,
                request.End,
                request.Type,
                request.Notes);

            Result<Guid> result = await sender.Send(command, ct);

            return result.Match(
                id => Results.Created($"/appointments/{id}", new { id }),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.AppointmentsCreate)
        .WithTags(Tags.Appointments)
        .WithName("CreateAppointment")
        .Produces<object>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
