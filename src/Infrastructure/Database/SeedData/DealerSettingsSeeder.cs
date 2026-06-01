using Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.SeedData;

public static class DealerSettingsSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        var exists = await context.DealerSettings
            .IgnoreQueryFilters()
            .AnyAsync(s => s.DealerId == Guid.Parse("11111111-1111-1111-1111-111111111111"), cancellationToken);

        if (exists)
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(@"
            INSERT INTO public.dealer_settings (
                id, dealer_id, dealer_name, contact_email, notifications_enabled, 
                updated_at, host_name, custom_domain, address, phone_number, 
                facebook_url, instagram_url, twitter_url, interest_rate_tna, 
                footer_text
            )
            VALUES (
                '22222222-2222-2222-2222-222222222222', 
                '11111111-1111-1111-1111-111111111111', 
                'Lux Dealership', 
                'info@luxdealership.com', 
                TRUE, 
                NOW(), 
                '127.0.0.1', 
                'localhost', 
                'Av. del Libertador 4500, Palermo, CABA', 
                '+54 11 9999-8888', 
                'https://facebook.com/luxdealership', 
                'https://instagram.com/luxdealership', 
                'https://twitter.com/luxdealership', 
                65.50, 
                '© 2024 Lux Dealership. Todos los derechos reservados.'
            )
            ON CONFLICT (dealer_id) DO NOTHING;
        ", cancellationToken);
    }
}
