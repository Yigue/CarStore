using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Sales.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.Commands.BackfillSaleCompletedCarStatus;

/// <summary>
/// Backfills <c>service_car = Vendido</c> for cars with completed sales (qa-p1-integridad D5).
///
/// Rule:
/// <c>service_car := Vendido WHERE dealer_id = @tenant AND service_car &lt;&gt; 'Vendido' AND EXISTS (completed sale for that car)</c>
///
/// Tenancy: scoped explicitly by <see cref="ICurrentTenantService.DealerId"/>.
/// Idempotent: re-running affects zero rows. Appends an audit row on every execution.
/// </summary>
internal sealed class BackfillSaleCompletedCarStatusCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<BackfillSaleCompletedCarStatusCommand, BackfillSaleCompletedCarStatusResult>
{
    public async Task<Result<BackfillSaleCompletedCarStatusResult>> Handle(
        BackfillSaleCompletedCarStatusCommand command,
        CancellationToken cancellationToken)
    {
        if (!command.DryRun && !command.Confirmed)
        {
            return Result.Failure<BackfillSaleCompletedCarStatusResult>(new Error(
                "Backfill.NotConfirmed",
                "Apply requires Confirmed=true. Use DryRun=true to preview.",
                ErrorType.Validation));
        }

        Guid dealerId = tenantService.DealerId;
        if (dealerId == Guid.Empty)
        {
            return Result.Failure<BackfillSaleCompletedCarStatusResult>(new Error(
                "Backfill.NoTenant",
                "Cannot run backfill without a resolved tenant.",
                ErrorType.Validation));
        }

        var stopwatch = Stopwatch.StartNew();
        var action = command.DryRun ? BackfillAction.DryRun : BackfillAction.Apply;

        List<Car> candidateCars = await context.Cars
            .Where(c => c.DealerId == dealerId &&
                        c.ServiceCar != StatusServiceCar.Vendido &&
                        context.Sales.Any(s => s.DealerId == dealerId && s.CarId == c.Id && s.Status == SaleStatus.Completed))
            .ToListAsync(cancellationToken);

        List<Guid> affectedCarIds = candidateCars.Select(c => c.Id).ToList();
        int affectedCount = affectedCarIds.Count;

        if (!command.DryRun && affectedCount > 0)
        {
            foreach (Car car in candidateCars)
            {
                car.MarkAsSold(dateTimeProvider.UtcNow);
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        stopwatch.Stop();

        var metadata = JsonSerializer.Serialize(affectedCarIds);
        BackfillAudit audit = BackfillAudit.Create(
            dealerId,
            userContext.UserId,
            action,
            affectedCount,
            (int)stopwatch.ElapsedMilliseconds,
            metadata,
            dateTimeProvider.UtcNow);

        context.BackfillAudits.Add(audit);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new BackfillSaleCompletedCarStatusResult(
            audit.Id,
            action,
            affectedCount,
            affectedCarIds));
    }
}
