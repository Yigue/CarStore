using Application.Appointments.Commands.DeleteAppointment;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Appointments;

internal sealed class Delete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("appointments/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var command = new DeleteAppointmentCommand(id);
            Result result = await sender.Send(command, ct);
            return result.Match(
                () => Results.NoContent(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.AppointmentsDelete)
        .WithTags(Tags.Appointments)
        .WithName("DeleteAppointment")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
