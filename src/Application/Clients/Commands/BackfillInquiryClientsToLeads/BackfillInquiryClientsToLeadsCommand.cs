using Application.Abstractions.Messaging;

namespace Application.Clients.Commands.BackfillInquiryClientsToLeads;

/// <summary>
/// Converts the Clients that the pre-lead public inquiry handler created back into Leads.
/// Mirrors the flag contract of the other admin backfills: <c>DryRun</c> previews,
/// <c>Confirmed</c> is required to write.
/// </summary>
public sealed record BackfillInquiryClientsToLeadsCommand(
    bool DryRun,
    bool Confirmed) : ICommand<BackfillInquiryClientsToLeadsResult>;
