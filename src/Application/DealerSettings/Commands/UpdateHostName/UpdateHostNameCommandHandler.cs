using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.DealerSettings;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.DealerSettings.Commands.UpdateHostName;

/// <summary>
/// Handler for <see cref="UpdateHostNameCommand"/>.
/// Loads the current tenant's DealerSettings with <c>IgnoreQueryFilters()</c>
/// to bypass the global DealerId filter (the tenant is resolved from the JWT
/// claim before this handler runs), calls <c>ChangeSlug</c> to validate RFC 1035
/// rules at the domain level, and persists the change.
///
/// Conflict (unique constraint) detection is left to the caller — if EF throws a
/// <see cref="DbUpdateException"/> for a UNIQUE violation, the endpoint maps it to
/// HTTP 409. RFC 1035 validation failures surface as <see cref="DomainException"/>
/// and are caught by <c>GlobalExceptionHandler</c> (HTTP 400).
/// </summary>
internal sealed class UpdateHostNameCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : ICommandHandler<UpdateHostNameCommand, DealerSettingsResponse>
{
    public async Task<Result<DealerSettingsResponse>> Handle(
        UpdateHostNameCommand command,
        CancellationToken cancellationToken)
    {
        DealerSettingsEntity? settings = await context.DealerSettings
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(s => s.DealerId == tenantService.DealerId, cancellationToken);

        if (settings is null)
        {
            return Result.Failure<DealerSettingsResponse>(DealerSettingsErrors.NotFound);
        }

        // Domain method validates RFC 1035; throws DomainException on invalid input.
        settings.ChangeSlug(command.Slug, command.HostName);

        await context.SaveChangesAsync(cancellationToken);

        return MapToResponse(settings);
    }

    private static DealerSettingsResponse MapToResponse(DealerSettingsEntity s) =>
        new()
        {
            Id = s.Id,
            DealerId = s.DealerId,
            DealerName = s.DealerName,
            ContactEmail = s.ContactEmail,
            NotificationsEnabled = s.NotificationsEnabled,
            UpdatedAt = s.UpdatedAt,
            HostName = s.HostName,
            Slug = s.Slug,
            IsActive = s.IsActive,
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
        };
}
