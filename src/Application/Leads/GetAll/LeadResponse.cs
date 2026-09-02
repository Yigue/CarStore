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
    Guid? InterestedVehicleId,
    string? InterestedVehicleName,
    Guid? ConvertedClientId,
    LeadLossReason? LossReason,
    string? Notes,
    string Source,
    DateTime CreatedAt,
    /// <summary>
    /// Whether a live quote already backs this lead — one raised against the lead itself, or
    /// against the client it was converted into. The board needs it to stop asking for a quote
    /// that exists: dragging to Negociación opened the quote form unconditionally, so a lead
    /// left behind by an older data state could never be moved forward, only re-quoted.
    /// </summary>
    bool HasQuote
);
