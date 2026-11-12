using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.DealerSettings;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.DealerSettings.Commands.SetBusinessHours;

internal sealed class SetBusinessHoursCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : ICommandHandler<SetBusinessHoursCommand, Guid>
{
    public async Task<Result<Guid>> Handle(SetBusinessHoursCommand command, CancellationToken cancellationToken)
    {
        Domain.DealerSettings.DealerSettings? dealerSettings = await context.DealerSettings
            .FirstOrDefaultAsync(ds => ds.DealerId == tenantService.DealerId, cancellationToken);

        if (dealerSettings is null)
        {
            return Result.Failure<Guid>(DealerSettingsErrors.NotFound);
        }

        try
        {
            dealerSettings.SetBusinessHours(
                command.Entries?
                    .Select(e => new BusinessHoursEntry(e.DayOfWeek, e.OpenTime, e.CloseTime))
                    .ToList());

            await context.SaveChangesAsync(cancellationToken);

            return Result.Success(dealerSettings.Id);
        }
        catch (DomainException ex)
        {
            return Result.Failure<Guid>(Error.Validation("DealerSettings.InvalidBusinessHours", ex.Message));
        }
    }
}
