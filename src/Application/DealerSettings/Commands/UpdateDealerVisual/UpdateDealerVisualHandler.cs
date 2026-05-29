using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.DealerSettings;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.DealerSettings.Commands.UpdateDealerVisual;

internal sealed class UpdateDealerVisualCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : ICommandHandler<UpdateDealerVisualCommand, Guid>
{
    public async Task<Result<Guid>> Handle(UpdateDealerVisualCommand command, CancellationToken cancellationToken)
    {
        var dealerSettings = await context.DealerSettings
            .FirstOrDefaultAsync(ds => ds.DealerId == tenantService.DealerId, cancellationToken);

        if (dealerSettings is null)
        {
            return Result.Failure<Guid>(DealerSettingsErrors.NotFound);
        }

        try
        {
            dealerSettings.UpdateVisual(
                command.LogoUrl,
                command.PrimaryColor,
                command.SecondaryColor,
                command.FooterText);

            await context.SaveChangesAsync(cancellationToken);

            return Result.Success(dealerSettings.Id);
        }
        catch (DomainException ex)
        {
            return Result.Failure<Guid>(Error.Validation("DealerSettings.InvalidVisual", ex.Message));
        }
    }
}