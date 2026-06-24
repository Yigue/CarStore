namespace Application.Users.Queries.GetAllUsers;

public sealed record UserListResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string Role,
    bool IsActive,
    DateTime CreatedAt
);