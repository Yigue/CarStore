using SharedKernel;

namespace Domain.Users;

public sealed class UserPermission : Entity
{
    private UserPermission() { }

    public UserPermission(Guid userId, string permission)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Permission = permission;
    }

    public Guid UserId { get; private set; }
    public string Permission { get; private set; }
}
