using SharedKernel;

namespace Domain.Appointments.Events;

public sealed record AppointmentCreatedDomainEvent(
    Guid AppointmentId,
    Guid AgentId,
    DateTime Start) : IDomainEvent;
