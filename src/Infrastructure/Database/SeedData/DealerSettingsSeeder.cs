using Domain.DealerSettings;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.SeedData;

public static class DealerSettingsSeeder
{
    private static readonly Guid DefaultDealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DefaultSettingsId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        var exists = await context.DealerSettings
            .IgnoreQueryFilters()
            .AnyAsync(s => s.DealerId == DefaultDealerId, cancellationToken);

        if (exists)
        {
            return;
        }

        // Use EF Core API (cross-DB: works on both PostgreSQL and SQLite used in tests).
        // HostName is set to "localhost" so the default test-client Host header resolves correctly.
        var settings = new DealerSettings(
            dealerId: DefaultDealerId,
            dealerName: "Lux Dealership",
            contactEmail: "info@luxdealership.com",
            notificationsEnabled: true,
            hostName: "localhost",
            customDomain: "lux.localhost",
            address: "Av. del Libertador 4500, Palermo, CABA",
            phoneNumber: "+54 11 9999-8888",
            facebookUrl: "https://facebook.com/luxdealership",
            instagramUrl: "https://instagram.com/luxdealership",
            twitterUrl: "https://twitter.com/luxdealership",
            interestRateTna: 65.50m,
            slug: "lux",
            isActive: true);

        context.DealerSettings.Add(settings);
        await context.SaveChangesAsync(cancellationToken);
    }
}
