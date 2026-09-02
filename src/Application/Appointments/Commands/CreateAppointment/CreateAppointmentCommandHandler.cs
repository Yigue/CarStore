using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Appointments;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Appointments.Commands.CreateAppointment;

/// <summary>
/// Conflict-detection algorithm (CRITICAL):
/// Two half-open intervals [start, end) overlap when <c>existing.Start &lt; new.End AND existing.End &gt; new.Start</c>.
/// We scope the query to <c>DealerId</c> (tenant isolation) and OR the per-resource collision
/// on <c>VehicleId</c> or <c>AgentId</c>. Backed by the composite index
/// <c>ix_appointments_dealer_time_range</c> over <c>(dealer_id, start_date_time, end_date_time)</c>.
/// </summary>
internal sealed class CreateAppointmentCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<CreateAppointmentCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateAppointmentCommand command, CancellationToken cancellationToken)
    {
        Guid dealerId = tenantService.DealerId;

        bool hasConflict = await context.Appointments
            .AsNoTracking()
            .AnyAsync(a =>
                a.DealerId == dealerId &&
                (a.VehicleId == command.VehicleId || a.AgentId == command.AgentId) &&
                a.StartDateTime < command.End &&
                a.EndDateTime > command.Start,
                cancellationToken);

        if (hasConflict)
            return Result.Failure<Guid>(AppointmentErrors.Conflict);

        try
        {
            var appointment = Appointment.Create(
                dealerId,
                command.VehicleId,
                command.ClientId,
                command.LeadId,
                command.AgentId,
                command.Start,
                command.End,
                command.Type,
                command.Notes,
                dateTimeProvider.UtcNow);

            context.Appointments.Add(appointment);

            if (command.LeadId is { } leadId && command.Type == AppointmentType.TestDrive)
            {
                Lead? lead = await context.Leads.FirstOrDefaultAsync(l => l.Id == leadId, cancellationToken);
                if (lead is not null && (lead.Status == LeadStatus.Nuevo || lead.Status == LeadStatus.Contactado))
                {
                    lead.ForceStatus(LeadStatus.Demostracion);
                }
            }

            await context.SaveChangesAsync(cancellationToken);

            return Result.Success(appointment.Id);
        }
        catch (DomainException ex)
        {
            return Result.Failure<Guid>(Error.Validation("Appointment.DomainError", ex.Message));
        }
    }
}
