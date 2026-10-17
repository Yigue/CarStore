using Application.Appointments.Commands.ConfirmAppointment;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Appointments;

/// <summary>sfc B5.2.3: confirms a Requested appointment by assigning an agent. 409 on conflict.</summary>
internal sealed class Confirm : IEndpoint
{
    public sealed record Request(Guid AgentId);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("appointments/{id:guid}/confirm", async (Guid id, Request request, ISender sender, CancellationToken ct) =>
        {
            var command = new ConfirmAppointmentCommand(id, request.AgentId);
            Result result = await sender.Send(command, ct);
            return result.Match(
                () => Results.Ok(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.AppointmentsUpdate)
        .WithTags(Tags.Appointments)
        .WithName("ConfirmAppointment")
        .Produces(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
