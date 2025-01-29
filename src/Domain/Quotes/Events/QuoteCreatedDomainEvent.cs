using SharedKernel;

namespace Domain.Quotes.Events;

public sealed record QuoteCreatedDomainEvent(
    Guid QuoteId,
    Guid CarId,
    Guid ClientId,
    decimal ProposedPrice) : IDomainEvent;
