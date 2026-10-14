using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Deposits;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Deposits.Refund;

/// <summary>
/// sfc B1.3.5 / RN-003: Active or Expired → Refunded. No money moves through the system. An Active deposit stops
/// holding its car, which is released unless an accepted quote or a pending sale still holds it (H6).
/// </summary>
internal sealed class RefundDepositCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    ICurrentTenantService tenantService)
    : ICommandHandler<RefundDepositCommand>
{
    public async Task<Result> Handle(RefundDepositCommand command, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return guard;

        Deposit? deposit = await context.Deposits.FirstOrDefaultAsync(d => d.Id == command.DepositId, cancellationToken);
        if (deposit is null || deposit.DealerId != tenantService.DealerId)
            return Result.Failure(DepositErrors.NotFound(command.DepositId));

        bool wasActive = deposit.Status == DepositStatus.Active;
        DateTime now = dateTimeProvider.UtcNow;

        Result refunded = deposit.Refund(command.Amount, command.Reason, now);
        if (refunded.IsFailure)
            return refunded;

        if (wasActive)
        {
            Car? car = await context.Cars.FirstOrDefaultAsync(c => c.Id == deposit.CarId, cancellationToken);
            if (car is { ServiceCar: StatusServiceCar.Reservado }
                && !await context.IsHeldBeyondDepositsAsync(car.Id, cancellationToken))
            {
                car.Release(now);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
