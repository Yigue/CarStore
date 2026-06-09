using Domain.Leads;

namespace Application.Leads.GetById;

public sealed record LeadDetailResponse(
    Guid Id,
    string ClientName,
    string Email,
    string Phone,
    LeadStatus Status,
    string StatusDisplay,
    Guid? AssignedAgentId,
    string? AssignedAgentName,
    Guid? InterestedVehicleId,
    string? InterestedVehicleName,
    Guid? ConvertedClientId,
    LeadLossReason? LossReason,
    string? Notes,
    string Source,
    DateTime CreatedAt
);
