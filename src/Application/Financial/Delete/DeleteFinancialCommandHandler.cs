using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Financial;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Financial.Delete;

internal sealed class DeleteFinancialCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : ICommandHandler<DeleteFinancialCommand, Guid>
{
    public async Task<Result<Guid>> Handle(DeleteFinancialCommand command, CancellationToken cancellationToken)
    {
        FinancialTransaction? financial = await context.Transactions
            .SingleOrDefaultAsync(f => f.Id == command.Id, cancellationToken);

        if (financial is null)
        {
            return Result.Failure<Guid>(FinancialErrors.NotFound(command.Id));
        }

        // REQ-FIN-TENANT-001: defense-in-handler for delete.
        var guard = TenantGuard.EnsureSameDealer(tenantService, financial.DealerId);
        if (guard.IsFailure)
        {
            return Result.Failure<Guid>(guard.Error);
        }

        context.Transactions.Remove(financial);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(command.Id);
    }
}
