using SharedKernel;

namespace Domain.Appointments.Events;

public sealed record AppointmentRescheduledDomainEvent(
    Guid AppointmentId,
    DateTime NewStart,
    DateTime NewEnd) : IDomainEvent;
