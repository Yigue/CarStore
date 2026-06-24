using Application.Appointments.Queries.GetAppointments;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Appointments;

internal sealed class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("appointments",
            async ([FromQuery] DateTime from, [FromQuery] DateTime to, ISender sender, CancellationToken ct) =>
            {
                var query = new GetAppointmentsQuery(from, to);
                Result<IReadOnlyList<AppointmentDto>> result = await sender.Send(query, ct);
                return result.Match(
                    list => Results.Ok(list),
                    CustomResults.Problem);
            })
        .HasPermission(Permissions.AppointmentsRead)
        .WithTags(Tags.Appointments)
        .WithName("GetAppointments")
        .Produces<IReadOnlyList<AppointmentDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
