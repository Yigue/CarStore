using Application.Abstractions.Messaging;

namespace Application.Leads.AssignAgent;

public sealed record AssignAgentToLeadCommand(Guid LeadId, Guid AgentId) : ICommand;
