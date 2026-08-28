using Domain.Cars;

namespace Application.Clients.Commands.BackfillInquiryClientsToLeads;

/// <summary>Outcome of the inquiry-clients-to-leads backfill.</summary>
public sealed record BackfillInquiryClientsToLeadsResult(
    Guid AuditId,
    BackfillAction Action,
    int AffectedRowCount,
    IReadOnlyList<Guid> ConvertedClientIds,
    int ReassignedQuoteCount);
