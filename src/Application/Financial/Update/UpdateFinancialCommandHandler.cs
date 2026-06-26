using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Financial;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Financial.Update;

internal sealed class UpdateFinancialCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : ICommandHandler<UpdateFinancialCommand, Guid>
{
    public async Task<Result<Guid>> Handle(UpdateFinancialCommand command, CancellationToken cancellationToken)
    {
        FinancialTransaction financial = await context.Transactions
            .SingleOrDefaultAsync(f => f.Id == command.Id, cancellationToken);

        if (financial is null)
        {
            return Result.Failure<Guid>(FinancialErrors.NotFound(command.Id));
        }

        // REQ-FIN-TENANT-001: defense-in-handler. The EF GQF already filters by
        // DealerId, but a cross-tenant attacker with a known tx id could still
        // reach this code via raw SQL or after the GQF is bypassed. Reject
        // BEFORE the Update + SaveChanges so no DB write occurs.
        var guard = TenantGuard.EnsureSameDealer(tenantService, financial.DealerId);
        if (guard.IsFailure)
        {
            return Result.Failure<Guid>(guard.Error);
        }

        financial.Update(
            command.Type,
            command.Amount,
            command.Description,
            command.PaymentMethod,
            command.ReferenceNumber,
            command.TransactionDate,
            command.CategoryId,
            command.CarId,
            command.ClientId,
            command.SaleId
        );

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(financial.Id);
    }
}
