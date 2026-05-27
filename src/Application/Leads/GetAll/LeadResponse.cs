using Domain.Leads;

namespace Application.Leads.GetAll;

public sealed record LeadResponse(
    Guid Id,
    string ClientName,
    string Email,
    string Phone,
    LeadStatus Status,
    string StatusDisplay,
    Guid? AssignedAgentId,
    string? AssignedAgentName,
    string? Notes,
    string Source,
    DateTime CreatedAt
);
