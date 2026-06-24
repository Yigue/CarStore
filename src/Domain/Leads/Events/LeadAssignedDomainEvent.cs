using SharedKernel;

namespace Domain.Leads.Events;

public sealed record LeadAssignedDomainEvent(Guid LeadId, Guid AgentId) : IDomainEvent;
