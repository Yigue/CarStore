using SharedKernel;

namespace Domain.Leads.Events;

public sealed record LeadCreatedDomainEvent(Guid LeadId, Guid? AssignedAgentId) : IDomainEvent;
