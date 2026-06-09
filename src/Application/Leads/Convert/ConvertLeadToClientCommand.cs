using Application.Abstractions.Messaging;

namespace Application.Leads.Convert;

public sealed record ConvertLeadToClientCommand(
    Guid LeadId,
    string Dni,
    string Address) : ICommand<Guid>;
