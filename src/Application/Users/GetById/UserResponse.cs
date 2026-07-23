using Domain.Users;

namespace Application.Users.GetById;

public sealed record UserResponse
{
    public Guid Id { get; init; }

    public string Email { get; init; }

    public string FirstName { get; init; }

    public string LastName { get; init; }

    public string? Phone { get; init; }

    public Guid RoleId { get; init; }

    public bool IsActive { get; init; }

    public DateTime CreatedAt { get; init; }

    public IEnumerable<string> Permissions { get; init; } = [];
}
