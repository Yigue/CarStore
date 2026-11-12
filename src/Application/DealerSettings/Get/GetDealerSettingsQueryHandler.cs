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
        //
        // Loaded as the entity rather than projected straight into the response: the carousel
        // needs DealerSettings.GetCarouselSlides() to turn the raw jsonb column into typed
        // slides, and that deserialization cannot be translated into SQL by a .Select().
        Domain.DealerSettings.DealerSettings? entity = await context.DealerSettings
            .Where(s => s.DealerId == tenantService.DealerId)
            .SingleOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            return Result.Failure<DealerSettingsResponse>(DealerSettingsErrors.NotFound);
        }

        var settings = new DealerSettingsResponse
        {
            Id = entity.Id,
            DealerId = entity.DealerId,
            DealerName = entity.DealerName,
            ContactEmail = entity.ContactEmail,
            NotificationsEnabled = entity.NotificationsEnabled,
            UpdatedAt = entity.UpdatedAt,
            HostName = entity.HostName,
            Slug = entity.Slug,
            IsActive = entity.IsActive,
            CustomDomain = entity.CustomDomain,
            Address = entity.Address,
            PhoneNumber = entity.PhoneNumber,
            FacebookUrl = entity.FacebookUrl,
            InstagramUrl = entity.InstagramUrl,
            TwitterUrl = entity.TwitterUrl,
            InterestRateTna = entity.InterestRateTna,
            LogoUrl = entity.LogoUrl,
            PrimaryColor = entity.PrimaryColor,
            SecondaryColor = entity.SecondaryColor,
            FooterText = entity.FooterText,
            HistoryText = entity.HistoryText,
            MissionText = entity.MissionText,
            VisionText = entity.VisionText,
            ValuesText = entity.ValuesText,
            CarouselSlides = entity.GetCarouselSlides()
                .Select(cs => new CarouselSlideResponse(cs.ImageUrl, cs.Title, cs.Subtitle, cs.Description, cs.CtaText, cs.CtaLink))
                .ToList(),
        };

        return settings;
    }
}
