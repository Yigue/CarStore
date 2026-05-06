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
    /// <summary>
    /// Default development DealerId. Must match the dealer seeded in DealersSeeder (or used in dev data).
    /// </summary>
    private static readonly Guid DefaultDealerId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// Seedea el usuario administrador por defecto.
    /// </summary>
    public static async Task SeedAsync(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        const string adminEmail = "admin@carstore.com";

        // Read DealerId from configuration, fallback to default development value
        var dealerIdConfig = configuration["AdminSeedDealerId"];
        var dealerId = !string.IsNullOrEmpty(dealerIdConfig) && Guid.TryParse(dealerIdConfig, out var parsedDealerId)
            ? parsedDealerId
            : DefaultDealerId;

        // Read password from configuration (environment variable ADMIN_SEED_PASSWORD)
        string adminPassword = configuration["ADMIN_SEED_PASSWORD"] ?? "Admin123!";

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            // This case is now less likely but good to keep as fallback
            adminPassword = GenerateRandomPassword();
            logger.LogWarning(
                "ADMIN_SEED_PASSWORD not configured. Generated random admin password: {Password}. " +
                "Store this securely and set ADMIN_SEED_PASSWORD for future deployments.",
                adminPassword);
        }

        // Verificar si ya existe el usuario admin (bypass tenant filter for seeding)
        var admin = context.Users
            .IgnoreQueryFilters()
            .FirstOrDefault(u => u.Email == adminEmail);

        if (admin is null)
        {
            // Hash de la contraseÃ±a
            var passwordHash = passwordHasher.Hash(adminPassword);

            // Crear usuario admin with valid DealerId
            admin = new User(
                dealerId,
                adminEmail,
                "Admin",
                "User",
                passwordHash);

            context.Users.Add(admin);
            await context.SaveChangesAsync(cancellationToken);
        }

        // Seeder de permisos para el admin (bypass tenant filter for seeding)
        if (!context.UserPermissions
            .IgnoreQueryFilters()
            .Any(p => p.UserId == admin.Id))
        {
            var permissions = new List<string>
            {
                "cars:read", "cars:create", "cars:update", "cars:delete",
                "clients:read", "clients:create", "clients:update", "clients:delete",
                "sales:read", "sales:create", "sales:update", "sales:delete",
                "quotes:read", "quotes:create", "quotes:update", "quotes:delete", "quotes:accept", "quotes:reject",
                "financial:read", "financial:create", "financial:update", "financial:delete",
                "users:read", "users:create", "users:access"
            };

            foreach (var permission in permissions)
            {
                context.UserPermissions.Add(new UserPermission(admin.Id, permission));
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

        // Ensure at least one of each required character type
        password[0] = uppercase[randomBytes[0] % uppercase.Length];
        password[1] = lowercase[randomBytes[1] % lowercase.Length];
        password[2] = digits[randomBytes[2] % digits.Length];
        password[3] = special[randomBytes[3] % special.Length];

        // Fill the rest randomly
        for (int i = 4; i < length; i++)
        {
            password[i] = allChars[randomBytes[i] % allChars.Length];
        }

        // Shuffle using Fisher-Yates
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

