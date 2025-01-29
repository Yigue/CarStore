using Domain.Clients.Attributes;

namespace Application.Clients.GetAll;

public sealed record ClientResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string DNI,
    string Email,
    string Phone,
    string Address,
    ClientStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);
