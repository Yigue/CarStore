using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.DealerSettings;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.DealerSettings.Commands.UpdateDealerLanding;

internal sealed class UpdateDealerLandingCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : ICommandHandler<UpdateDealerLandingCommand, Guid>
{
    public async Task<Result<Guid>> Handle(UpdateDealerLandingCommand command, CancellationToken cancellationToken)
    {
        Domain.DealerSettings.DealerSettings? dealerSettings = await context.DealerSettings
            .FirstOrDefaultAsync(ds => ds.DealerId == tenantService.DealerId, cancellationToken);

        if (dealerSettings is null)
        {
            return Result.Failure<Guid>(DealerSettingsErrors.NotFound);
        }

        try
        {
            dealerSettings.UpdateLanding(
                command.HistoryText,
                command.MissionText,
                command.VisionText,
                command.ValuesText,
                command.CarouselSlides?
                    .Select(s => new CarouselSlide(s.ImageUrl, s.Title, s.Subtitle, s.Description, s.CtaText, s.CtaLink))
                    .ToList());

            await context.SaveChangesAsync(cancellationToken);

            return Result.Success(dealerSettings.Id);
        }
        catch (DomainException ex)
        {
            return Result.Failure<Guid>(Error.Validation("DealerSettings.InvalidLanding", ex.Message));
        }
    }
}
