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
)
{
    // ─── LEAD-05 · the 360° view ─────────────────────────────────────────────────────────────
    //
    // The dashboard has rendered these sections for a while. They never appeared, because this
    // response carried none of them: `detail.client`, `detail.quotes` and `detail.appointments`
    // were always undefined and the whole block was dead markup. The ask was to make the related
    // records navigable; first they have to exist.
    //
    // Each DTO carries the id of the entity it describes, which is what the links are built from.
    //
    // Declared as init-only PROPERTIES, not as positional parameters: the projection in
    // GetLeadByIdQueryHandler is an expression tree, and an expression tree cannot call a
    // constructor that has optional arguments. They are filled in with a `with` expression once
    // the lead itself has been read.
    public LeadClientDto? Client { get; init; }

    public IReadOnlyList<LeadQuoteDto> Quotes { get; init; } = [];

    public IReadOnlyList<LeadAppointmentDto> Appointments { get; init; } = [];

    public IReadOnlyList<LeadSaleDto> Sales { get; init; } = [];
}

public sealed record LeadClientDto(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    string Status);

public sealed record LeadQuoteDto(
    Guid Id,
    Guid CarId,
    string CarName,
    decimal ProposedPrice,
    string Status,
    DateTime CreatedAt);

public sealed record LeadAppointmentDto(
    Guid Id,
    DateTime ScheduledAt,
    string Type,
    string Status,
    string Notes);

public sealed record LeadSaleDto(
    Guid Id,
    Guid CarId,
    string CarName,
    decimal FinalPrice,
    string Status,
    DateTime SaleDate);
