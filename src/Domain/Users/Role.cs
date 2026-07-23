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
        var existing = _permissions.FirstOrDefault(p => p.Permission == permission);
        if (existing is not null)
        {
            _permissions.Remove(existing);
        }
    }
}
