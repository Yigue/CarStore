using SharedKernel;

namespace Domain.Cars.Events;

public sealed record CarDeleteDomainEvent(Guid CarId) : IDomainEvent;
