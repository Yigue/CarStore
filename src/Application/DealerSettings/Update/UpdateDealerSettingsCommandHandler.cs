using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.DealerSettings;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.DealerSettings.Update;

internal sealed class UpdateDealerSettingsCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : ICommandHandler<UpdateDealerSettingsCommand, DealerSettingsResponse>
{
    public async Task<Result<DealerSettingsResponse>> Handle(
        UpdateDealerSettingsCommand command,
        CancellationToken cancellationToken)
    {
        DealerSettingsEntity? settings = await context.DealerSettings
            .SingleOrDefaultAsync(s => s.DealerId == tenantService.DealerId, cancellationToken);

        // Upsert: si no existe (primera vez), crear; si existe, actualizar.
        if (settings is null)
        {
            settings = new DealerSettingsEntity(
                tenantService.DealerId,
                command.DealerName,
                command.ContactEmail,
                command.NotificationsEnabled,
                command.HostName,
                command.CustomDomain,
                command.Address,
                command.PhoneNumber,
                command.FacebookUrl,
                command.InstagramUrl,
                command.TwitterUrl,
                command.InterestRateTna);
            context.DealerSettings.Add(settings);
        }
        else
        {
            settings.Update(
                command.DealerName, 
                command.ContactEmail, 
                command.NotificationsEnabled,
                command.HostName,
                command.CustomDomain,
                command.Address,
                command.PhoneNumber,
                command.FacebookUrl,
                command.InstagramUrl,
                command.TwitterUrl,
                command.InterestRateTna);
        }

        await context.SaveChangesAsync(cancellationToken);

        return new DealerSettingsResponse
        {
            Id = settings.Id,
            DealerId = settings.DealerId,
            DealerName = settings.DealerName,
            ContactEmail = settings.ContactEmail,
            NotificationsEnabled = settings.NotificationsEnabled,
            UpdatedAt = settings.UpdatedAt,
            HostName = settings.HostName,
            Slug = settings.Slug,
            IsActive = settings.IsActive,
            CustomDomain = settings.CustomDomain,
            Address = settings.Address,
            PhoneNumber = settings.PhoneNumber,
            FacebookUrl = settings.FacebookUrl,
            InstagramUrl = settings.InstagramUrl,
            TwitterUrl = settings.TwitterUrl,
            InterestRateTna = settings.InterestRateTna
        };
    }
}
