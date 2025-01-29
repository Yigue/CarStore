using SharedKernel;

namespace Domain.Cars.Events;

public sealed record NewCarDomainEvent(Guid CarId) : IDomainEvent;
