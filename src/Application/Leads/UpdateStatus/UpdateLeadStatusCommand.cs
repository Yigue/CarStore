using Application.Abstractions.Messaging;
using Domain.Leads;

namespace Application.Leads.UpdateStatus;

public sealed record UpdateLeadStatusCommand(
    Guid LeadId,
    LeadStatus? NewStatus,
    string? Notes = null,
    LeadLossReason? LossReason = null) : ICommand;
