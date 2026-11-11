namespace Domain.Users;

/// <summary>
/// spr PR 4.1 (SPR-18, D6): single source of truth for the three default roles every dealer
/// gets — <c>Admin</c>, <c>Empleado</c>, <c>Cliente</c> — and the permission set each one
/// seeds with.
///
/// <para>
/// Before this PR the same three permission lists were duplicated inline across
/// <c>ProvisionDealerCommandHandler</c> (onboarding), <c>UsersSeeder</c> (demo/dev seed data)
/// and <c>RegisterUserCommandHandler</c> (self-registration's implicit Cliente role) — free to
/// drift apart, and they already had (see <see cref="AdminPermissions"/> remarks). This
/// catalogue is the one place that defines "what Admin/Empleado/Cliente can do"; the backfill
/// command (<c>BackfillDefaultRolesCommandHandler</c>) uses <see cref="GetPermissions"/> and
/// <see cref="Sync"/> to converge every existing dealer onto it.
/// </para>
///
/// <para>
/// Not to be confused with <see cref="PermissionCatalog"/>: that one lists every permission a
/// dealership is allowed to grant to a <em>custom</em> role via the role-editing screen (CFG-03).
/// This one lists what the three <em>default</em> roles are seeded with out of the box — a
/// narrower, deliberately different concern.
/// </para>
/// </summary>
public static class DefaultRoleCatalog
{
    public const string Admin = "Admin";
    public const string Empleado = "Empleado";
    public const string Cliente = "Cliente";

    /// <summary>The three default role names, in seeding order.</summary>
    public static readonly IReadOnlyList<string> RoleNames = [Admin, Empleado, Cliente];

    private static readonly IReadOnlyDictionary<string, string> DescriptionsByRole = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Admin] = "Administrador del Sistema",
        [Empleado] = "Empleado Demo",
        [Cliente] = "Cliente registrado",
    };

    /// <summary>
    /// Permission set of the default Admin role, shared by <c>ProvisionDealerCommandHandler</c>
    /// (dealer onboarding) and <c>UsersSeeder</c> (dev seed).
    /// <para>
    /// <c>"audit:read"</c> belongs here. spr PR 4.1 dropped it as "drift" because onboarding had never
    /// granted it, but sfc B2.2 had added the dealer audit trail (<c>GET audit-log</c> and the
    /// dashboard's Auditoría page) a week earlier, gated on exactly that permission and meant for
    /// the dealer's Admin. With it gone no default role could open that page — the missing grant in
    /// onboarding was the bug, not the one in the seeder.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyList<string> AdminPermissionsList =
    [
        "cars:read", "cars:create", "cars:update", "cars:delete",
        "clients:read", "clients:write", "clients:delete",
        "sales:read", "sales:create", "sales:update", "sales:delete",
        "quotes:read", "quotes:create", "quotes:update", "quotes:delete", "quotes:accept", "quotes:reject",
        "financial:read", "financial:create", "financial:update", "financial:delete",
        "users:read", "users:create", "users:access", "CanManageUsers", "CanManageRoles",
        "CanManageSettings",
        "leads:read", "leads:create", "leads:update", "leads:delete",
        "leads:write", "leads:archive",
        "appointments:read", "appointments:create", "appointments:update", "appointments:delete",
        "admin:backfill",
        "webhooks:manage",
        // sfc B2.2: the dealer's own audit trail (GET audit-log, dashboard Auditoría).
        "audit:read",
        // qa-p1-integridad PR6 (D7): grant at both seed sites.
        "documents:read", "documents:create",
        // sfc B4.2 / RN-010: Admin sees/manages every salesperson's commission.
        "commissions:read-own", "commissions:read-all", "commissions:manage",
    ];

    /// <summary>
    /// Exact permission set captured from <c>UsersSeeder.SeedEmpleadoAsync</c> — the only site
    /// that defines an Empleado role today.
    /// </summary>
    private static readonly IReadOnlyList<string> EmpleadoPermissionsList =
    [
        "cars:read",
        "clients:read",
        "sales:read",
        "sales:create",
        "leads:read",
        "appointments:read",
        "documents:read",
        "documents:create",
        // sfc B4.2 / RN-010: a salesperson sees their own commission by default, never another's.
        "commissions:read-own",
    ];

    /// <summary>
    /// Cliente has no default permissions anywhere today (<c>ProvisionDealerCommandHandler</c>,
    /// <c>UsersSeeder.SeedClienteAsync</c> and <c>RegisterUserCommandHandler</c> all create it bare).
    /// </summary>
    private static readonly IReadOnlyList<string> ClientePermissionsList = [];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> PermissionsByRole = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        [Admin] = AdminPermissionsList,
        [Empleado] = EmpleadoPermissionsList,
        [Cliente] = ClientePermissionsList,
    };

    /// <summary>True when <paramref name="roleName"/> is one of the three catalogued defaults.</summary>
    public static bool IsDefaultRole(string roleName) => PermissionsByRole.ContainsKey(roleName);

    /// <summary>
    /// The exact permission set a default role must have. Throws for any name outside
    /// <see cref="RoleNames"/> — callers must check <see cref="IsDefaultRole"/> first for
    /// roles that might be dealer-custom.
    /// </summary>
    public static IReadOnlyList<string> GetPermissions(string roleName)
    {
        if (!PermissionsByRole.TryGetValue(roleName, out IReadOnlyList<string>? permissions))
        {
            throw new ArgumentOutOfRangeException(
                nameof(roleName),
                roleName,
                $"'{roleName}' is not a default role. Expected one of: {string.Join(", ", RoleNames)}.");
        }

        return permissions;
    }

    /// <summary>The description a fresh default role is seeded with.</summary>
    public static string GetDescription(string roleName) => DescriptionsByRole[roleName];

    /// <summary>
    /// Builds a brand-new <paramref name="roleName"/> role for <paramref name="dealerId"/> with
    /// exactly the catalogued permission set already attached. Used by every seed/onboarding
    /// site that needs to create one of the three default roles from scratch.
    /// </summary>
    public static Role BuildRole(Guid dealerId, string roleName)
    {
        IReadOnlyList<string> permissions = GetPermissions(roleName);
        var role = new Role(dealerId, roleName, GetDescription(roleName));
        foreach (string permission in permissions)
        {
            role.AddPermission(permission);
        }

        return role;
    }

    /// <summary>
    /// Makes an existing default role's permissions exactly match the catalogue — adds what is
    /// missing and removes anything the role has that the catalogue does not list. No-op for a
    /// role whose name is not a catalogued default (a dealer-custom role is left untouched).
    /// Calling this twice in a row is idempotent: the second call is always a no-op.
    /// </summary>
    public static void Sync(Role role)
    {
        if (!IsDefaultRole(role.Name))
        {
            return;
        }

        role.ReplacePermissions(GetPermissions(role.Name));
    }

    /// <summary>
    /// Permissions <paramref name="role"/> is missing relative to the catalogue, and permissions
    /// it has that the catalogue does not list — the dry-run report for one role. Empty for a
    /// role whose name is not a catalogued default.
    /// </summary>
    public static (IReadOnlyList<string> Missing, IReadOnlyList<string> Extra) Diff(Role role)
    {
        if (!IsDefaultRole(role.Name))
        {
            return ([], []);
        }

        var current = role.Permissions.Select(p => p.Permission).ToHashSet(StringComparer.Ordinal);
        var catalog = GetPermissions(role.Name).ToHashSet(StringComparer.Ordinal);

        List<string> missing = catalog.Except(current).OrderBy(p => p, StringComparer.Ordinal).ToList();
        List<string> extra = current.Except(catalog).OrderBy(p => p, StringComparer.Ordinal).ToList();

        return (missing, extra);
    }
}
