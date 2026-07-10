using Application.Abstractions.Messaging;
using Domain.Clients.Attributes;

namespace Application.Clients.Create;

public sealed record CreateClientCommand(
    string FirstName,
    string LastName,
    string DNI,
    string Email,
    string Phone,
    string Address,
    Domain.Clients.Attributes.ClientType Type,
    string? City = null,
    string? ZipCode = null,
    string? Notes = null) : ICommand<Guid>;
