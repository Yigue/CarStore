using SharedKernel;

namespace Domain.Cars.Events;

public sealed record CarChangeStatusDomainEvent(Guid CarId) : IDomainEvent;
