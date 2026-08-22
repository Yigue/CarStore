using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Financial;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Financial.Categories.Delete;

internal sealed class DeleteCategoryCommandHandler(
    IApplicationDbContext context,
    Application.Abstractions.Caching.ICachedCategoryService cachedCategoryService)
    : ICommandHandler<DeleteCategoryCommand>
{
    public async Task<Result> Handle(DeleteCategoryCommand command, CancellationToken cancellationToken)
    {
        var category = await context.TransactionCategories
            .FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);

        if (category is null)
        {
            return Result.Failure(Error.NotFound("Category.NotFound", "The category was not found."));
        }

        // D3 (qa-p1-integridad PR2): layer 1, the common case. Polite pre-check that also
        // names the blocking count in the response.
        var blockingTransactionCount = await context.Transactions
            .CountAsync(t => t.CategoryId == command.Id, cancellationToken);

        if (blockingTransactionCount > 0)
        {
            return Result.Failure(FinancialErrors.CategoryInUse(blockingTransactionCount));
        }

        context.TransactionCategories.Remove(category);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsForeignKeyViolation(ex))
        {
            // D3: layer 2, the race the pre-check cannot close. A transaction inserted
            // between the check above and this DELETE takes a FOR KEY SHARE lock on the
            // category row; Postgres blocks this DELETE until that insert commits, then
            // fails it with SQLSTATE 23503. The exact blocking count is not re-queryable
            // here without re-introducing the same race, so this reports "at least 1" —
            // the observable contract (409, never 500, no data lost) is what matters.
            return Result.Failure(FinancialErrors.CategoryInUse(1));
        }

        await cachedCategoryService.InvalidateCacheAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// Detects a foreign key violation (SQLSTATE 23503) via reflection so the Application
    /// layer does not take a package dependency on Npgsql, mirroring
    /// <c>Web.Api/Endpoints/DealerSettings/UpdateHostName.cs</c>'s unique-constraint check
    /// (SQLSTATE 23505) for the same reason.
    /// </summary>
    private static bool IsForeignKeyViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null)
        {
            return false;
        }

        if (inner.GetType().Name == "PostgresException")
        {
            var sqlState = inner.GetType().GetProperty("SqlState")?.GetValue(inner) as string;
            return sqlState == "23503";
        }

        return inner.Message.Contains("FOREIGN KEY constraint failed", StringComparison.OrdinalIgnoreCase);
    }
}
