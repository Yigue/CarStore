namespace Application.Queries.Users.GetAll;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    Guid TenantId);