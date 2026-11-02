using SharedKernel;

namespace Domain.Cars.Events;

/// <summary>sfc C1.2 / ADR C25: raised by <see cref="Car.UpdatePrice(decimal, DateTime, Domain.Shared.ValueObjects.CurrencyCode?)"/>
/// only when the price actually changes; drives the MercadoLibre push connector (pause the listing).</summary>
public sealed record CarPriceChangedDomainEvent(Guid CarId) : IDomainEvent;
