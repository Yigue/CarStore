using Application.Abstractions.Messaging;
using Domain.Leads;

namespace Application.Leads.Create;

public sealed record CreateLeadCommand(
    string ClientName,
    string Email,
    string Phone,
    LeadSource Source,
    string? Notes
) : ICommand<Guid>;
