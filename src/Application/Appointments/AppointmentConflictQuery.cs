using Domain.Appointments;
using Microsoft.EntityFrameworkCore;

namespace Application.Appointments;

/// <summary>
/// sfc B5.2.3 / H15: single home of the appointment overlap rule, used by create, reschedule and confirm.
/// Half-open intervals overlap when <c>existing.Start &lt; end AND existing.End &gt; start</c>.
/// Without an agent only the vehicle collides; with one, the vehicle OR the agent.
/// Cancelled and no-show appointments free their slot.
/// </summary>
internal static class AppointmentConflictQuery
{
    public static Task<bool> HasConflictAsync(
        this IQueryable<Appointment> appointments,
        Guid dealerId,
        Guid vehicleId,
        Guid? agentId,
        DateTime start,
        DateTime end,
        Guid? excludeAppointmentId,
        CancellationToken cancellationToken) =>
        appointments
            .AsNoTracking()
            .AnyAsync(a =>
                a.DealerId == dealerId &&
                (excludeAppointmentId == null || a.Id != excludeAppointmentId) &&
                a.Status != AppointmentStatus.Cancelled &&
                a.Status != AppointmentStatus.NoShow &&
                (a.VehicleId == vehicleId || (agentId != null && a.AgentId == agentId)) &&
                a.StartDateTime < end &&
                a.EndDateTime > start,
                cancellationToken);
}
