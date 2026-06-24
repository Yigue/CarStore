using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.DealerSettings;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.DealerSettings.Get;

internal sealed class GetDealerSettingsQueryHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : IQueryHandler<GetDealerSettingsQuery, DealerSettingsResponse>
{
    public async Task<Result<DealerSettingsResponse>> Handle(
        GetDealerSettingsQuery query,
        CancellationToken cancellationToken)
    {
        // El query filter ya filtra por DealerId, pero asumimos que existe una sola fila.
        DealerSettingsResponse? settings = await context.DealerSettings
            .Where(s => s.DealerId == tenantService.DealerId)
            .Select(s => new DealerSettingsResponse
            {
                Id = s.Id,
                DealerId = s.DealerId,
                DealerName = s.DealerName,
                ContactEmail = s.ContactEmail,
                NotificationsEnabled = s.NotificationsEnabled,
                UpdatedAt = s.UpdatedAt,
                HostName = s.HostName,
                CustomDomain = s.CustomDomain,
                Address = s.Address,
                PhoneNumber = s.PhoneNumber,
                FacebookUrl = s.FacebookUrl,
                InstagramUrl = s.InstagramUrl,
                TwitterUrl = s.TwitterUrl,
                InterestRateTna = s.InterestRateTna,
                LogoUrl = s.LogoUrl,
                PrimaryColor = s.PrimaryColor,
                SecondaryColor = s.SecondaryColor,
                FooterText = s.FooterText
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            return Result.Failure<DealerSettingsResponse>(DealerSettingsErrors.NotFound);
        }

        return settings;
    }
}
