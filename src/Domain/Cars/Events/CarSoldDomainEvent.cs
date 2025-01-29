using SharedKernel;

namespace Domain.Cars.Events;

public sealed record CarSoldDomainEvent(Guid CarId) : IDomainEvent;
