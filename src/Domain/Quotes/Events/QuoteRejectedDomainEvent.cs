using SharedKernel;

namespace Domain.Quotes.Events;

public sealed record QuoteRejectedDomainEvent(
    Guid QuoteId,
    string Reason) : IDomainEvent;
