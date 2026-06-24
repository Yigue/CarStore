using SharedKernel;

namespace Domain.Leads.Events;

public sealed record LeadStatusChangedDomainEvent(
    Guid LeadId,
    LeadStatus OldStatus,
    LeadStatus NewStatus) : IDomainEvent;
