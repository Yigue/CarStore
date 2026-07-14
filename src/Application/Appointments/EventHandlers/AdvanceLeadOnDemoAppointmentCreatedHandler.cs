using Application.Abstractions.Data;
using Domain.Appointments;
using Domain.Appointments.Events;
using Domain.Leads;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Appointments.EventHandlers;

/// <summary>
/// Demo appointment automation: when a <see cref="AppointmentType.TestDrive"/> appointment
/// is created for a Lead, auto-advance that lead to <see cref="LeadStatus.Demostracion"/>.
/// Mirrors the system-driven transition pattern in
/// <c>Application.Leads.UpdateStatus.UpdateLeadStatusFromQuoteHandler</c>: uses
/// <see cref="Lead.ForceStatus"/> to bypass the sequential UI-enforcement rules
/// (e.g. the "Demostracion requires InterestedVehicleId" guard in
/// <see cref="Lead.UpdateStatus"/>), while still respecting the Ganado no-regression
/// invariant baked into ForceStatus itself.
/// <para>
/// Forward-only: leads already at or past Demostracion (Negociacion/Ganado/Perdido/
/// Archivado) are left untouched. Appointments not linked to a Lead (client-only) are
/// skipped — there is no CRM pipeline to advance. Appointment types other than
/// TestDrive (Service, Delivery) don't represent a "demo" step in v1 and are skipped too.
/// </para>
/// </summary>
internal sealed class AdvanceLeadOnDemoAppointmentCreatedHandler(
    IApplicationDbContext context,
    ILogger<AdvanceLeadOnDemoAppointmentCreatedHandler> logger)
    : INotificationHandler<AppointmentCreatedDomainEvent>
{
    public async Task Handle(AppointmentCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        Appointment? appointment = await context.Appointments
            .FirstOrDefaultAsync(a => a.Id == notification.AppointmentId, cancellationToken);

        if (appointment is null)
        {
            logger.LogWarning(
                "AppointmentCreatedDomainEvent for missing Appointment {AppointmentId}; skipping demo auto-advance.",
                notification.AppointmentId);
            return;
        }

        if (appointment.LeadId is null)
        {
            // No Lead attached (client-only appointment) — nothing to advance.
            return;
        }

        if (appointment.Type != AppointmentType.TestDrive)
        {
            return;
        }

        Lead? lead = await context.Leads
            .FirstOrDefaultAsync(l => l.Id == appointment.LeadId.Value, cancellationToken);

        if (lead is null)
        {
            logger.LogWarning(
                "Appointment {AppointmentId} references missing Lead {LeadId}; skipping demo auto-advance.",
                appointment.Id,
                appointment.LeadId);
            return;
        }

        if (lead.Status != LeadStatus.Nuevo && lead.Status != LeadStatus.Contactado)
        {
            // Already at/past Demostracion, or in a terminal state (Ganado/Perdido/Archivado) — no-op.
            return;
        }

        lead.ForceStatus(LeadStatus.Demostracion);
        await context.SaveChangesAsync(cancellationToken);
    }
}
