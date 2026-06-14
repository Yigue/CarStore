namespace Application.Queries.Clients.GetIncomplete;

public sealed record IncompleteClientResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email);
