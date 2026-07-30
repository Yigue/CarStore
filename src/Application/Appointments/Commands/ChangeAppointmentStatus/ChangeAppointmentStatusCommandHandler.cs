using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Appointments.Commands.ChangeAppointmentStatus;

internal sealed class ChangeAppointmentStatusCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : ICommandHandler<ChangeAppointmentStatusCommand>
{
    public async Task<Result> Handle(
        ChangeAppointmentStatusCommand request,
        CancellationToken cancellationToken)
    {
        Guid dealerId = tenantService.DealerId;

        Appointment? appointment = await context.Appointments
            .FirstOrDefaultAsync(a => a.Id == request.AppointmentId && a.DealerId == dealerId, cancellationToken);

        if (appointment is null)
        {
            return Result.Failure(AppointmentErrors.NotFound(request.AppointmentId));
        }

        try
        {
            switch (request.TargetStatus)
            {
                case AppointmentStatus.Completed:
                    appointment.Complete();
                    break;
                case AppointmentStatus.Cancelled:
                    appointment.Cancel();
                    break;
                case AppointmentStatus.NoShow:
                    appointment.MarkNoShow();
                    break;
                default:
                    return Result.Failure(AppointmentErrors.InvalidStatusTransition(appointment.Status, request.TargetStatus));
            }
        }
        catch (DomainException)
        {
            return Result.Failure(AppointmentErrors.InvalidStatusTransition(appointment.Status, request.TargetStatus));
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
