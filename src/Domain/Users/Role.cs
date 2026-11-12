using Domain.Shared;
using SharedKernel;

namespace Domain.Users;

public sealed class Role : Entity
{
    private Role()
    {
        _permissions = [];
    }

    public Role(Guid dealerId, string name, string description)
    {
        SetDealer(dealerId);
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        _permissions = [];
    }

    public string Name { get; private set; }
    public string Description { get; private set; }

    private readonly List<RolePermission> _permissions;
    public IReadOnlyCollection<RolePermission> Permissions => _permissions;

    public void AddPermission(string permission)
    {
        if (_permissions.Any(p => p.Permission == permission))
            return;

        _permissions.Add(new RolePermission(Id, permission));
    }

    public void RemovePermission(string permission)
    {
        var existing = _permissions.Find(p => p.Permission == permission);
        if (existing is not null)
        {
            _permissions.Remove(existing);
        }
    }

    /// <summary>CFG-03: a dealership may rename the roles it defined.</summary>
    public void Rename(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Role name cannot be empty");

        Name = name;
        Description = description ?? string.Empty;
    }

    /// <summary>
    /// CFG-03: makes the role's permissions exactly <paramref name="permissions"/>.
    ///
    /// <para>
    /// The matrix sends what the role should now be able to do, not a diff, so this is a
    /// replacement rather than a merge — otherwise unticking a box would do nothing and a
    /// permission could only ever be added. Rows that survive are kept rather than deleted and
    /// re-inserted, so EF writes only the actual difference.
    /// </para>
    /// </summary>
    public void ReplacePermissions(IEnumerable<string> permissions)
    {
        var desired = permissions.Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

        _permissions.RemoveAll(p => !desired.Contains(p.Permission));

        foreach (string permission in desired)
        {
            AddPermission(permission);
        }
    }
}
