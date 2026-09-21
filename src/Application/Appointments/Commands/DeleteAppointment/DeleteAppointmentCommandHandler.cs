using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Appointments.Commands.DeleteAppointment;

/// <summary>
/// Hard delete: appointments are operational records with no audit requirement
/// beyond the outbox event trail. Soft-delete is intentionally out of scope.
/// </summary>
internal sealed class DeleteAppointmentCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService) : ICommandHandler<DeleteAppointmentCommand>
{
    public async Task<Result> Handle(DeleteAppointmentCommand command, CancellationToken cancellationToken)
    {
        Guid dealerId = tenantService.DealerId;

        Appointment? appointment = await context.Appointments
            .FirstOrDefaultAsync(a => a.Id == command.AppointmentId && a.DealerId == dealerId, cancellationToken);

        if (appointment is null)
            return Result.Failure(AppointmentErrors.NotFound(command.AppointmentId));

        context.Appointments.Remove(appointment);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
