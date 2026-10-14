using Application.Abstractions.Data;
using Domain.Quotes.Attributes;
using Domain.Sales.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Application.Deposits;

/// <summary>
/// sfc B1.3 / H6: a deposit is not necessarily the only thing holding a Reservado car. An accepted quote or a pending
/// sale also does, and the car must stay Reservado while either exists.
/// </summary>
internal static class CarHoldQueries
{
    public static async Task<bool> IsHeldBeyondDepositsAsync(
        this IApplicationDbContext context, Guid carId, CancellationToken cancellationToken)
    {
        bool heldByQuote = await context.Quotes.AnyAsync(
            q => q.CarId == carId && q.Status == QuoteStatus.Accepted, cancellationToken);
        if (heldByQuote)
            return true;

        return await context.Sales.AnyAsync(s => s.CarId == carId && s.Status == SaleStatus.Pending, cancellationToken);
    }
}
