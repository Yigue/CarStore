using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Sales;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sales.Void;

/// <summary>
/// sfc B1.2 / ADR C3 / RN-006: voids a Completed sale. A sale of another dealer is a 404. The car is reverted by
/// <c>SaleVoidedDomainEventHandler</c>. Voiding frees the row from <c>ux_sales_one_completed_per_car</c>
/// (filter <c>status = 'Completed'</c>), so the car can be sold again.
/// </summary>
internal sealed class VoidSaleCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    ICurrentTenantService tenantService,
    IDateTimeProvider dateTimeProvider)
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

        Result result = sale.Void(command.Reason, userContext.UserId, dateTimeProvider.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
