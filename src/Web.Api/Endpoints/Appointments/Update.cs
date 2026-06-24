using Application.Appointments.Commands.RescheduleAppointment;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Appointments;

/// <summary>
/// PUT semantics here are pragmatic: rescheduling moves an appointment to a new
/// time slot. Conflict detection in the handler excludes the appointment being
/// updated. Returns 200 on success, 404 if not found, 409 on conflict.
/// </summary>
internal sealed class Update : IEndpoint
{
    public sealed record Request(DateTime Start, DateTime End);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("appointments/{id:guid}", async (Guid id, Request request, ISender sender, CancellationToken ct) =>
        {
            var command = new RescheduleAppointmentCommand(id, request.Start, request.End);
            Result result = await sender.Send(command, ct);
            return result.Match(
                () => Results.Ok(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.AppointmentsUpdate)
        .WithTags(Tags.Appointments)
        .WithName("RescheduleAppointment")
        .Produces(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
