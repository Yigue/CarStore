using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Deposits.Get;

/// <summary>sfc B1.3.5: pinned to the caller's dealer in the handler as well as by the global query filter.</summary>
internal sealed class GetDepositsQueryHandler(IApplicationDbContext context, ICurrentTenantService tenantService)
    : IQueryHandler<GetDepositsQuery, IReadOnlyList<DepositResponse>>
{
    public async Task<Result<IReadOnlyList<DepositResponse>>> Handle(GetDepositsQuery query, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return Result.Failure<IReadOnlyList<DepositResponse>>(guard.Error);

        Guid dealerId = tenantService.DealerId;
        var deposits = context.Deposits.AsNoTracking().Where(d => d.DealerId == dealerId);
        if (query.CarId is { } carId)
            deposits = deposits.Where(d => d.CarId == carId);

        IReadOnlyList<DepositResponse> rows = await deposits
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new DepositResponse(
                d.Id,
                d.CarId,
                d.ClientId,
                d.LeadId,
                d.QuoteId,
                d.Amount.Amount,
                d.Amount.Currency,
                d.Status.ToString(),
                d.CreatedAtUtc,
                d.ExpiresAtUtc,
                d.RefundedAmount != null ? d.RefundedAmount.Amount : null,
                d.RefundReason,
                d.AppliedSaleId,
                d.ExchangeRate != null ? d.ExchangeRate.Rate : null))
            .ToListAsync(cancellationToken);

        return Result.Success(rows);
    }
}
