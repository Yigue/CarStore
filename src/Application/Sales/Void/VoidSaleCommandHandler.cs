using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Cars;
using Domain.Sales;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sales.Void;

/// <summary>
/// sfc B1.2 / ADR C3 / RN-006: voids a Completed sale. A sale of another dealer is a 404. The car is reverted in the same
/// unit of work (one save); if it is missing or not sold the void fails and nothing is saved. Voiding frees the row from <c>ux_sales_one_completed_per_car</c>
/// (filter <c>status = 'Completed'</c>), so the car can be sold again. The dealer audit entry (actor + reason) is written by
/// <c>TenantAuditInterceptor</c> in that same save (sfc B2.2.4).
/// </summary>
internal sealed class VoidSaleCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    ICurrentTenantService tenantService,
    IDateTimeProvider dateTimeProvider,
    Application.Abstractions.Auditing.IAuditContext auditContext)
    : ICommandHandler<VoidSaleCommand>
{
    public async Task<Result> Handle(VoidSaleCommand command, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
        {
            return guard;
        }

        Guid dealerId = tenantService.DealerId;
        Sale? sale = await context.Sales
            .SingleOrDefaultAsync(s => s.Id == command.Id && s.DealerId == dealerId, cancellationToken);

        if (sale is null)
        {
            return Result.Failure(SalesErrors.NotFound(command.Id));
        }

        DateTime now = dateTimeProvider.UtcNow;
        Result result = sale.Void(command.Reason, userContext.UserId, now);
        if (result.IsFailure)
        {
            return result;
        }

        // Same unit of work as the void (single SaveChangesAsync below): a Voided sale with a still-Vendido car is
        // the inconsistency this prevents. Pinned to the sale's dealer. On failure nothing is saved (the scoped context is discarded).
        Car? car = await context.Cars
            .SingleOrDefaultAsync(c => c.Id == sale.CarId && c.DealerId == dealerId, cancellationToken);

        if (car is null)
        {
            return Result.Failure(CarErrors.NotFound(sale.CarId));
        }

        Result revert = car.RevertSale(sale.Id, now);
        if (revert.IsFailure)
        {
            return revert;
        }

        // sfc B2.2.4: the business reason travels to TenantAuditInterceptor, which writes the Sale.Status audit entry
        // inside this same SaveChanges (one transaction with the void and the car revert). The actor comes from IUserContext.
        auditContext.SetReason($"Sale.Void: {command.Reason.Trim()}");

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
