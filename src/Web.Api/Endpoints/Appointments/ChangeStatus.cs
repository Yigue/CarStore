using Application.Appointments.Commands.ChangeAppointmentStatus;
using Domain.Appointments;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Appointments;

internal sealed class ChangeStatus : IEndpoint
{
    public sealed record Request(AppointmentStatus Status);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("appointments/{id:guid}/status", async (Guid id, Request request, ISender sender, CancellationToken ct) =>
        {
            var command = new ChangeAppointmentStatusCommand(id, request.Status);
            Result result = await sender.Send(command, ct);
            return result.Match(
                () => Results.Ok(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.AppointmentsUpdate)
        .WithTags(Tags.Appointments)
        .WithName("ChangeAppointmentStatus")
        .Produces(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
