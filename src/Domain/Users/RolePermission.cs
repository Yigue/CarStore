namespace Domain.Users;

public sealed class RolePermission
{
    private RolePermission() { }

    public RolePermission(Guid roleId, string permission)
    {
        RoleId = roleId;
        Permission = permission;
    }

    public Guid RoleId { get; private set; }
    public string Permission { get; private set; } = string.Empty;
}
