using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Financial.GetAll;

internal sealed class GetAllFinancialsQueryHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : IQueryHandler<GetAllFinancialsQuery, IReadOnlyList<FinancialResponses>>
{
    public async Task<Result<IReadOnlyList<FinancialResponses>>> Handle(GetAllFinancialsQuery query, CancellationToken cancellationToken)
    {
        // REQ-FIN-TENANT-001: short-circuit before any DB round-trip if the
        // request lacks a tenant context (e.g. an anonymous migration job).
        var guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
        {
            return Result.Failure<IReadOnlyList<FinancialResponses>>(guard.Error);
        }

        List<FinancialResponses> transactions = await context.Transactions
            .AsNoTracking()
            .Select(transaction => new FinancialResponses(
                transaction.Id,
                transaction.Type,
                transaction.Amount.Amount,
                transaction.Description,
                transaction.PaymentMethod,
                transaction.ReferenceNumber,
                transaction.TransactionDate,
                transaction.CategoryId,
                transaction.Category != null ? transaction.Category.Name : null,
                transaction.CarId,
                transaction.Car != null ? transaction.Car.Marca.Nombre + " " + transaction.Car.Modelo.Nombre : null,
                transaction.ClientId,
                transaction.Client != null ? transaction.Client.FirstName + " " + transaction.Client.LastName : null,
                transaction.SaleId,
                transaction.TransactionDate,
                transaction.TransactionDate,
                transaction.DealerId
            ))
            .ToListAsync(cancellationToken);

        return Result.Success((IReadOnlyList<FinancialResponses>)transactions);
    }
}
