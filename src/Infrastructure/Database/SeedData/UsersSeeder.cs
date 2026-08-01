using System.Security.Cryptography;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Database.SeedData;

/// <summary>
/// Seeder para Usuarios del sistema.
/// </summary>
internal static class UsersSeeder
{
    private static readonly Guid DefaultDealerId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static async Task SeedAsync(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        const string adminEmail = "admin@carstore.com";

        var dealerIdConfig = configuration["AdminSeedDealerId"];
        var dealerId = !string.IsNullOrEmpty(dealerIdConfig) && Guid.TryParse(dealerIdConfig, out var parsedDealerId)
            ? parsedDealerId
            : DefaultDealerId;

        string adminPassword = configuration["ADMIN_SEED_PASSWORD"] ?? "Admin123!";
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            adminPassword = GenerateRandomPassword();
            logger.LogWarning(
                "ADMIN_SEED_PASSWORD not configured. Generated random admin password: {Password}. " +
                "Store this securely and set ADMIN_SEED_PASSWORD for future deployments.",
                adminPassword);
        }

        var adminRole = await SeedRoleAsync(context, dealerId, "Admin", "Administrador del Sistema", new[]
        {
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
            // qa-p1-integridad PR6 (D7): granted by no role anywhere until now —
            // GET /documents/{id}/download-url has been 403 for every seeded user since it shipped.
            "documents:read", "documents:create"
        }, cancellationToken);

        var admin = context.Users
            .IgnoreQueryFilters()
            .FirstOrDefault(u => u.Email == adminEmail);

        if (admin is null)
        {
            var passwordHash = passwordHasher.Hash(adminPassword);
            admin = new User(dealerId, adminEmail, "Admin", "User", passwordHash, adminRole.Id);
            context.Users.Add(admin);
            await context.SaveChangesAsync(cancellationToken);
        }
        else if (admin.RoleId != adminRole.Id)
        {
            admin.UpdateRole(adminRole.Id);
            await context.SaveChangesAsync(cancellationToken);
        }

        await SeedEmpleadoAsync(context, passwordHasher, configuration, dealerId, cancellationToken);
        await SeedSuperAdminAsync(context, passwordHasher, configuration, logger, cancellationToken);
    }

    private static async Task<Role> SeedRoleAsync(
        IApplicationDbContext context, 
        Guid dealerId, 
        string roleName, 
        string description, 
        string[] permissions,
        CancellationToken cancellationToken)
    {
        var role = await context.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.DealerId == dealerId && r.Name == roleName, cancellationToken);

        if (role == null)
        {
            role = new Role(dealerId, roleName, description);
            foreach (var permission in permissions)
            {
                role.AddPermission(permission);
            }
            context.Roles.Add(role);
            await context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var existingPermissions = await context.RolePermissions
                .IgnoreQueryFilters()
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.Permission)
                .ToListAsync(cancellationToken);

            bool added = false;
            foreach (var permission in permissions)
            {
                if (!existingPermissions.Contains(permission))
                {
                    role.AddPermission(permission);
                    added = true;
                }
            }
            if (added)
            {
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        return role;
    }

    private static async Task SeedEmpleadoAsync(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        Guid dealerId,
        CancellationToken cancellationToken)
    {
        const string empleadoEmail = "empleado@carstore.com";
        string empleadoPassword = configuration["EMPLEADO_SEED_PASSWORD"] ?? "Empleado123!";

        // REQ-QT-RBAC-001: Cotizaciones is Admin-only by default (Etapa 3) —
        // "quotes:read"/"quotes:create" removed from the Empleado seed.
        // REQ-SL-RBAC-001: Sales edit/delete is Admin-only by default —
        // "sales:update" removed from the Empleado seed (sales-lifecycle-hardening).
        // Both only affect fresh seeds / new dealers: SeedRoleAsync only adds
        // missing permissions on re-seed, it never revokes existing grants
        // (ADR-8/ADR-9, accepted risk — see design.md §2).
        var empleadoRole = await SeedRoleAsync(context, dealerId, "Empleado", "Empleado Demo", new[]
        {
            "cars:read",
            "clients:read",
            "sales:read",
            "sales:create",
            "leads:read",
            "appointments:read",
            // qa-p1-integridad PR6 (D7): Empleado uploads/downloads documents in normal
            // operation — must not be silently 403'd once PR7 requires the permission.
            "documents:read",
            "documents:create"
        }, cancellationToken);

        var empleado = context.Users
            .IgnoreQueryFilters()
            .FirstOrDefault(u => u.Email == empleadoEmail);

        if (empleado is null)
        {
            var passwordHash = passwordHasher.Hash(empleadoPassword);
            empleado = new User(dealerId, empleadoEmail, "Empleado", "Demo", passwordHash, empleadoRole.Id);
            context.Users.Add(empleado);
            await context.SaveChangesAsync(cancellationToken);
        }
        else if (empleado.RoleId != empleadoRole.Id)
        {
            empleado.UpdateRole(empleadoRole.Id);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedSuperAdminAsync(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        const string superAdminEmail = "superadmin@carstore.com";
        string superAdminPassword = configuration["SUPER_ADMIN_SEED_PASSWORD"] ?? "SuperAdmin123!";

        var superAdmin = context.Users
            .IgnoreQueryFilters()
            .FirstOrDefault(u => u.Email == superAdminEmail);

        if (superAdmin is null)
        {
            var passwordHash = passwordHasher.Hash(superAdminPassword);
            superAdmin = User.CreateSuperAdmin(superAdminEmail, "Super", "Admin", passwordHash);
            context.Users.Add(superAdmin);
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("SuperAdmin seeded with email {Email}", superAdminEmail);
        }
        else if (superAdmin.RoleId != Guid.Empty)
        {
            superAdmin.UpdateRole(Guid.Empty);
            await context.SaveChangesAsync(cancellationToken);
        }

        if (superAdmin.DealerId != Guid.Empty)
        {
            logger.LogError(
                "SuperAdmin user {Id} has non-empty DealerId {DealerId}. " +
                "This violates the platform isolation invariant.",
                superAdmin.Id, superAdmin.DealerId);
        }

        var platformPermissions = new List<string>
        {
            "platform:dealers:read",
            "platform:dealers:suspend",
            "platform:dealers:activate",
            "platform:metrics:read"
        };

        var existingPermissions = context.UserPermissions
            .IgnoreQueryFilters()
            .Where(p => p.UserId == superAdmin.Id)
            .Select(p => p.Permission)
            .ToHashSet();

        var missingPermissions = platformPermissions.Where(p => !existingPermissions.Contains(p)).ToList();
        if (missingPermissions.Count > 0)
        {
            foreach (var permission in missingPermissions)
            {
                context.UserPermissions.Add(new UserPermission(superAdmin.Id, permission));
            }
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static string GenerateRandomPassword(int length = 16)
    {
        const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowercase = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string special = "!@#$%^&*()_+-=";
        const string allChars = uppercase + lowercase + digits + special;

        Span<byte> randomBytes = stackalloc byte[length];
        RandomNumberGenerator.Fill(randomBytes);

        char[] password = new char[length];
        password[0] = uppercase[randomBytes[0] % uppercase.Length];
        password[1] = lowercase[randomBytes[1] % lowercase.Length];
        password[2] = digits[randomBytes[2] % digits.Length];
        password[3] = special[randomBytes[3] % special.Length];

        for (int i = 4; i < length; i++)
        {
            password[i] = allChars[randomBytes[i] % allChars.Length];
        }

        Span<byte> shuffleBytes = stackalloc byte[length];
        RandomNumberGenerator.Fill(shuffleBytes);
        for (int i = length - 1; i > 0; i--)
        {
            int j = shuffleBytes[i] % (i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }

        return new string(password);
    }
}
