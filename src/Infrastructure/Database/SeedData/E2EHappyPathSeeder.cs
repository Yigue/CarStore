using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Domain.Billing;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Database.SeedData;

/// <summary>
/// Fixtures for the happy-path E2E suite (spr Fase 8, B21): the handful of rows the specs need that the
/// regular dev seed does not provide, under stable identifiers so a spec can find them by name.
/// <list type="bullet">
/// <item><c>hp-suspend</c> dealer + admin — HP-10 suspends and reactivates it, so no other spec depends on it.</item>
/// <item><c>hp-dunning</c> dealer + admin (owner) with a known Stripe customer id — the dunning spec sends
/// <c>invoice.payment_failed</c>/<c>trial_will_end</c> for it and reads the email; the subscription is put back to Active on every run.</item>
/// <item><c>hp17.reset@carstore.test</c> — HP-17 resets this user's password; its password is rewritten on every run.</item>
/// <item>vehicle <c>HP05</c> + lead <c>HP06 Lead</c> on the demo dealer — HP-05 / HP-06.</item>
/// <item>an Active subscription for the demo dealer, whatever the rest of the seed did.</item>
/// </list>
/// Opt-in with <c>Seeding:E2E:Enabled</c>; idempotent. It refuses to run outside Development/Testing: these
/// accounts have a published password.
/// </summary>
public static class E2EHappyPathSeeder
{
    public const string ConfigKey = "Seeding:E2E:Enabled";

    public static readonly Guid DemoDealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SuspendDealerId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    public static readonly Guid DunningDealerId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    public const string DunningDealerSlug = "hp-dunning";
    public const string DunningDealerName = "HP Dunning Dealer";
    public const string DunningAdminEmail = "hp.dunning@carstore.test";
    /// <summary>Known Stripe customer id: invoice events only carry the customer, so the spec needs to know it.</summary>
    public const string DunningStripeCustomerId = "cus_hp_dunning";

    public const string SuspendDealerSlug = "hp-suspend";
    public const string SuspendDealerName = "HP Suspend Dealer";
    public const string SuspendAdminEmail = "hp.suspend@carstore.test";
    public const string ResetUserEmail = "hp17.reset@carstore.test";
    public const string ResetUserInitialPassword = "Reset123!old";
    public const string VehiclePatente = "HP505AA";
    public const string VehicleDescription = "HP05 vehiculo de prueba E2E";
    public const string LeadName = "HP06 Lead";

    public static bool IsEnabled(IConfiguration configuration) =>
        bool.TryParse(configuration[ConfigKey], out bool enabled) && enabled;

    public static async Task SeedAsync(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        string environmentName,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled(configuration))
        {
            return;
        }

        if (!string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{ConfigKey} is enabled in environment '{environmentName}'. The E2E fixtures create accounts with a " +
                "known password and may only run in Development or Testing.");
        }

        string adminPassword = configuration["ADMIN_SEED_PASSWORD"] ?? "Admin123!";

        await SeedSuspendDealerAsync(context, passwordHasher, adminPassword, cancellationToken);
        await SeedDunningDealerAsync(context, passwordHasher, adminPassword, cancellationToken);
        await SeedResetUserAsync(context, passwordHasher, cancellationToken);
        await SeedVehicleAndLeadAsync(context, cancellationToken);
        await EnsureDemoSubscriptionActiveAsync(context, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedSuspendDealerAsync(
        IApplicationDbContext context, IPasswordHasher passwordHasher, string password, CancellationToken ct)
    {
        if (context is not ApplicationDbContext db)
        {
            return;
        }

        if (!await db.DealerSettings.IgnoreQueryFilters().AnyAsync(s => s.DealerId == SuspendDealerId, ct))
        {
            db.DealerSettings.Add(new Domain.DealerSettings.DealerSettings(
                dealerId: SuspendDealerId,
                dealerName: SuspendDealerName,
                contactEmail: SuspendAdminEmail,
                notificationsEnabled: true,
                hostName: $"{SuspendDealerSlug}.localhost",
                slug: SuspendDealerSlug));
        }

        var role = await context.Roles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.DealerId == SuspendDealerId && r.Name == "Admin", ct);
        if (role is null)
        {
            role = new Domain.Users.Role(SuspendDealerId, "Admin", "Administrador del Sistema");
            context.Roles.Add(role);
        }

        if (!await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == SuspendAdminEmail, ct))
        {
            var admin = new Domain.Users.User(
                SuspendDealerId, SuspendAdminEmail, "HP", "Suspend", passwordHasher.Hash(password), role.Id);
            context.Users.Add(admin);
            db.DealerSettings.Local.FirstOrDefault(s => s.DealerId == SuspendDealerId && s.OwnerUserId is null)?.SetOwner(admin.Id);
        }

        if (!await context.DealerSubscriptions.IgnoreQueryFilters().AnyAsync(s => s.DealerId == SuspendDealerId, ct))
        {
            context.DealerSubscriptions.Add(
                SubscriptionStateSeed.CreateSeededSubscription(SuspendDealerId, SubscriptionStatus.Active));
        }
    }

    private static async Task SeedDunningDealerAsync(
        IApplicationDbContext context, IPasswordHasher passwordHasher, string password, CancellationToken ct)
    {
        if (context is not ApplicationDbContext db)
        {
            return;
        }

        if (!await db.DealerSettings.IgnoreQueryFilters().AnyAsync(s => s.DealerId == DunningDealerId, ct))
        {
            db.DealerSettings.Add(new Domain.DealerSettings.DealerSettings(
                dealerId: DunningDealerId,
                dealerName: DunningDealerName,
                contactEmail: DunningAdminEmail,
                notificationsEnabled: true,
                hostName: $"{DunningDealerSlug}.localhost",
                slug: DunningDealerSlug));
        }

        var role = await context.Roles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.DealerId == DunningDealerId && r.Name == "Admin", ct);
        if (role is null)
        {
            role = new Domain.Users.Role(DunningDealerId, "Admin", "Administrador del Sistema");
            context.Roles.Add(role);
        }

        if (!await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == DunningAdminEmail, ct))
        {
            var admin = new Domain.Users.User(
                DunningDealerId, DunningAdminEmail, "HP", "Dunning", passwordHasher.Hash(password), role.Id);
            context.Users.Add(admin);
            db.DealerSettings.Local.FirstOrDefault(s => s.DealerId == DunningDealerId && s.OwnerUserId is null)?.SetOwner(admin.Id);
        }

        var now = DateTime.UtcNow;
        var sub = await context.DealerSubscriptions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.DealerId == DunningDealerId, ct);
        if (sub is null)
        {
            sub = DealerSubscription.Create(DunningDealerId, DunningStripeCustomerId, "sub_hp_dunning", "price_hp_dunning");
            sub.Activate("sub_hp_dunning", now.AddDays(-1), now.AddDays(30));
            context.DealerSubscriptions.Add(sub);
        }
        else if (sub.Status != SubscriptionStatus.Active)
        {
            // The spec leaves it PastDue; every API start puts it back so the next run starts clean.
            sub.Activate("sub_hp_dunning", now.AddDays(-1), now.AddDays(30));
        }
    }

    private static async Task SeedResetUserAsync(
        IApplicationDbContext context, IPasswordHasher passwordHasher, CancellationToken ct)
    {
        var role = await context.Roles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.DealerId == DemoDealerId && r.Name == "Empleado", ct)
            ?? await context.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.DealerId == DemoDealerId, ct);
        if (role is null)
        {
            return;
        }

        var user = await context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == ResetUserEmail, ct);
        string hash = passwordHasher.Hash(ResetUserInitialPassword);
        if (user is null)
        {
            context.Users.Add(new Domain.Users.User(DemoDealerId, ResetUserEmail, "HP17", "Reset", hash, role.Id));
        }
        else
        {
            // A previous run left the password the spec chose; every run starts from the same one.
            user.SetPassword(hash);
        }
    }

    private static async Task SeedVehicleAndLeadAsync(IApplicationDbContext context, CancellationToken ct)
    {
        if (!await context.Cars.IgnoreQueryFilters().AnyAsync(c => c.Patente == VehiclePatente, ct))
        {
            var marca = await context.Marca.FirstOrDefaultAsync(m => m.Nombre == "Toyota", ct);
            var modelo = await context.Modelo.FirstOrDefaultAsync(m => m.Nombre == "Corolla", ct);
            if (marca is not null && modelo is not null)
            {
                context.Cars.Add(new Car(
                    DemoDealerId, marca, modelo, Color.White, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
                    4, 5, 1800, 0, 2024, VehiclePatente, VehicleDescription, 25000, DateTime.UtcNow));
            }
        }

        if (!await context.Leads.IgnoreQueryFilters().AnyAsync(l => l.ClientName == LeadName, ct))
        {
            context.Leads.Add(Lead.Create(
                DemoDealerId, LeadName, "hp06.lead@carstore.test", "+54 11 6000-0006", LeadSource.Web, DateTime.UtcNow));
        }
    }

    private static async Task EnsureDemoSubscriptionActiveAsync(IApplicationDbContext context, CancellationToken ct)
    {
        var sub = await context.DealerSubscriptions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.DealerId == DemoDealerId, ct);
        if (sub is null)
        {
            context.DealerSubscriptions.Add(
                SubscriptionStateSeed.CreateSeededSubscription(DemoDealerId, SubscriptionStatus.Active));
        }
        else if (sub.Status != SubscriptionStatus.Active)
        {
            var now = DateTime.UtcNow;
            sub.Activate(sub.StripeSubscriptionId ?? $"sub_{DemoDealerId:N}", now.AddDays(-1), now.AddDays(30));
        }
    }
}
