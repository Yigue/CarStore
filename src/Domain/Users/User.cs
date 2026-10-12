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
        Guid roleId)
    {
        _permissions = [];
        SetDealer(dealerId);
        Id = Guid.NewGuid();
        Email = new Email(email);
        FirstName = firstName;
        LastName = lastName;
        PasswordHash = passwordHash;
        RoleId = roleId;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;

        Raise(new UserRegisteredDomainEvent(Id));
    }

    /// <summary>
    /// Factory for the platform-level SuperAdmin user.
    /// SuperAdmin has DealerId = Guid.Empty by contract (ADR-1); this factory
    /// bypasses the non-empty DealerId invariant enforced by SetDealer().
    /// </summary>
    public static User CreateSuperAdmin(
        string email,
        string firstName,
        string lastName,
        string passwordHash)
    {
        // Use the private parameterless EF constructor — DealerId stays Guid.Empty.
        var user = new User();

        user.Id = Guid.NewGuid();
        user.Email = new Email(email);
        user.FirstName = firstName;
        user.LastName = lastName;
        user.PasswordHash = passwordHash;
        user.RoleId = Guid.Empty;
        user.IsActive = true;
        user.CreatedAt = DateTime.UtcNow;

        user.Raise(new UserRegisteredDomainEvent(user.Id));

        return user;
    }

    public Email Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string PasswordHash { get; private set; }
    public Guid RoleId { get; private set; }
    public bool IsActive { get; private set; }
    public string? Phone { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>sfc A4.1 / RN-030: when the user proved control of the address. Null = not verified.</summary>
    public DateTime? EmailVerifiedAtUtc { get; private set; }

    public bool IsEmailVerified => EmailVerifiedAtUtc is not null;

    /// <summary>
    /// Indicates whether the user is a platform-level SuperAdmin.
    /// SuperAdmin users are defined exclusively by DealerId == Guid.Empty.
    /// </summary>
    public bool IsPlatformAdmin => DealerId == Guid.Empty;

    // Navigation property for permissions (configured in EF Core)
    private readonly List<UserPermission> _permissions;
    public IReadOnlyCollection<UserPermission> Permissions => _permissions;

    public void SetPassword(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Password hash cannot be empty");

        PasswordHash = passwordHash;
    }

    /// <summary>Idempotent: the first verification timestamp is kept.</summary>
    public void MarkEmailVerified(DateTime nowUtc)
    {
        EmailVerifiedAtUtc ??= nowUtc;
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

    public void UpdateRole(Guid roleId)
    {
        RoleId = roleId;
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
        var existing = _permissions.Find(p => p.Permission == permission);
        if (existing is not null)
        {
            _permissions.Remove(existing);
        }
    }
}
