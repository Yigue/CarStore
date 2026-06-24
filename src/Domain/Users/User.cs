using Domain.Shared.ValueObjects;
using SharedKernel;

namespace Domain.Users;

public sealed class User : Entity
{
    private User()
    {
        _permissions = [];
    }

    public User(
        Guid dealerId,
        string email,
        string firstName,
        string lastName,
        string passwordHash,
        UserRole role = UserRole.Cliente)
    {
        _permissions = [];
        SetDealer(dealerId);
        Id = Guid.NewGuid();
        Email = new Email(email);
        FirstName = firstName;
        LastName = lastName;
        PasswordHash = passwordHash;
        Role = role;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;

        Raise(new UserRegisteredDomainEvent(Id));
    }

    public Email Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public string? Phone { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigation property for permissions (configured in EF Core)
    private readonly List<UserPermission> _permissions = [];
    public IReadOnlyCollection<UserPermission> Permissions => _permissions;

    public void SetPassword(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Password hash cannot be empty");

        PasswordHash = passwordHash;
    }

    public void UpdatePhone(string? phone)
    {
        Phone = phone;
    }

    public void UpdateName(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
    }

    public void UpdateRole(UserRole role)
    {
        Role = role;
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new DomainException("User is already deactivated");

        IsActive = false;
    }

    public void Activate()
    {
        if (IsActive)
            throw new DomainException("User is already active");

        IsActive = true;
    }

    public void AddPermission(string permission, Guid? grantedBy = null)
    {
        if (_permissions.Any(p => p.Permission == permission))
            return;

        _permissions.Add(new UserPermission(Id, permission, grantedBy));
    }

    public void RemovePermission(string permission)
    {
        var existing = _permissions.FirstOrDefault(p => p.Permission == permission);
        if (existing is not null)
        {
            _permissions.Remove(existing);
        }
    }
}
