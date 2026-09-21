using Domain.Appointments.Events;
using SharedKernel;

namespace Domain.Appointments;

/// <summary>
/// Appointment aggregate root. Represents a scheduled event (test drive, service, delivery)
/// between an agent, a client, and a vehicle. Tenant-scoped via <see cref="Entity.DealerId"/>.
/// Time-overlap conflict detection is performed at the application layer (CQRS handler)
/// against a dealer-wide range index. Local invariants: EndDateTime &gt; StartDateTime,
/// StartDateTime cannot be dated before the calling operation's own clock (no scheduling
/// into the past), and Reschedule/Complete/Cancel/MarkNoShow all require the appointment to
/// still be Scheduled (see <see cref="EnsureScheduled"/>) — once it left that status it is
/// a closed record.
/// </summary>
public sealed class Appointment : Entity
{
    public Guid VehicleId { get; private set; }
    public Guid? ClientId { get; private set; }
    public Guid? LeadId { get; private set; }
    public Guid AgentId { get; private set; }
    public DateTime StartDateTime { get; private set; }
    public DateTime EndDateTime { get; private set; }
    public AppointmentType Type { get; private set; }
    public AppointmentStatus Status { get; private set; } = AppointmentStatus.Scheduled;
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // EF Core
    private Appointment() { }

    public static Appointment Create(
        Guid dealerId,
        Guid vehicleId,
        Guid? clientId,
        Guid? leadId,
        Guid agentId,
        DateTime start,
        DateTime end,
        AppointmentType type,
        string? notes,
        DateTime createdAtUtc)
    {
        if (vehicleId == Guid.Empty)
            throw new DomainException("VehicleId cannot be empty");
        if (clientId is null && leadId is null)
            throw new DomainException("Un compromiso debe tener un Cliente o un Lead");
        if (agentId == Guid.Empty)
            throw new DomainException("AgentId cannot be empty");
        if (end <= start)
            throw new DomainException("La hora de fin debe ser posterior a la hora de inicio");
        if (start.Date < createdAtUtc.Date)
            throw new DomainException("No se puede agendar un turno en una fecha pasada");

        var appointment = new Appointment();
        appointment.SetDealer(dealerId);
        appointment.Id = Guid.NewGuid();
        appointment.VehicleId = vehicleId;
        appointment.ClientId = clientId;
        appointment.LeadId = leadId;
        appointment.AgentId = agentId;
        appointment.StartDateTime = start;
        appointment.EndDateTime = end;
        appointment.Type = type;
        appointment.Status = AppointmentStatus.Scheduled;
        appointment.Notes = notes;
        appointment.CreatedAt = createdAtUtc;

        appointment.Raise(new AppointmentCreatedDomainEvent(appointment.Id, agentId, start));
        return appointment;
    }

    public void Reschedule(DateTime newStart, DateTime newEnd, DateTime rescheduledAtUtc)
    {
        EnsureScheduled();

        if (newEnd <= newStart)
            throw new DomainException("La hora de fin debe ser posterior a la hora de inicio");
        if (newStart.Date < rescheduledAtUtc.Date)
            throw new DomainException("No se puede reagendar un turno a una fecha pasada");

        StartDateTime = newStart;
        EndDateTime = newEnd;

        Raise(new AppointmentRescheduledDomainEvent(Id, newStart, newEnd));
    }

    public void UpdateNotes(string? notes) => Notes = notes;

    /// <summary>
    /// Re-points this appointment to a client (e.g. when its lead is converted into a client).
    /// Clears the lead reference to keep a single party per appointment.
    /// </summary>
    public void AssignClient(Guid clientId)
    {
        if (clientId == Guid.Empty)
            throw new DomainException("ClientId cannot be empty when assigning an appointment to a client");

        ClientId = clientId;
        LeadId = null;
    }

    private void EnsureScheduled()
    {
        if (Status != AppointmentStatus.Scheduled)
            throw new DomainException($"No se puede modificar un turno en estado {Status}");
    }

    public void Complete()
    {
        EnsureScheduled();
        Status = AppointmentStatus.Completed;
    }

    public void Cancel()
    {
        EnsureScheduled();
        Status = AppointmentStatus.Cancelled;
    }

    public void MarkNoShow()
    {
        EnsureScheduled();
        Status = AppointmentStatus.NoShow;
    }
}
