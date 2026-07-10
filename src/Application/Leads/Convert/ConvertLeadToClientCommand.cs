using Application.Abstractions.Messaging;
using Domain.Clients.Attributes;

namespace Application.Leads.Convert;

public sealed record ConvertLeadToClientCommand(
    Guid LeadId,
    string Dni,
    string Address,
    Domain.Clients.Attributes.ClientType Type) : ICommand<Guid>;
