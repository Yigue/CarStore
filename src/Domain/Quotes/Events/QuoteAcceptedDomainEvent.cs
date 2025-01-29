using SharedKernel;

namespace Domain.Quotes.Events;

public sealed record QuoteAcceptedDomainEvent(Guid QuoteId) : IDomainEvent;
