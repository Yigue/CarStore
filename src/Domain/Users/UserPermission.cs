using SharedKernel;

namespace Domain.Users;

public sealed class UserPermission : Entity
{
    private UserPermission() { }

    public UserPermission(Guid userId, string permission, Guid? grantedBy = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Permission = permission;
        GrantedAt = DateTime.UtcNow;
        GrantedBy = grantedBy;
    }

    public Guid UserId { get; private set; }
    public string Permission { get; private set; }
    public DateTime GrantedAt { get; private set; }
    public Guid? GrantedBy { get; private set; }

    // Navigation property (configured in EF Core)
    private User? _user;
    public User? User => _user;
}
