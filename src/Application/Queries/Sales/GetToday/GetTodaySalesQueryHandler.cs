using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Queries.Sales.GetToday;

internal sealed class GetTodaySalesQueryHandler
    : IQueryHandler<GetTodaySalesQuery, TodaySalesResponse>
{
    private readonly IApplicationDbContext _context;

    public GetTodaySalesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<TodaySalesResponse>> Handle(
        GetTodaySalesQuery query,
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var todaySales = await _context.Sales
            .AsNoTracking()
            .Include(s => s.Car)
            .Include(s => s.Client)
            .Where(s => s.SaleDate >= today && s.SaleDate < tomorrow)
            .Select(s => new TodaySaleItem(
                s.Id,
                s.CarId,
                s.ClientId,
                s.FinalPrice.Amount,
                s.PaymentMethod.ToString(),
                s.Status.ToString(),
                s.ContractNumber,
                s.SaleDate,
                s.Comments,
                s.Car != null ? s.Car.Patente.Value : null,
                s.Client != null ? $"{s.Client.FirstName} {s.Client.LastName}" : null))
            .ToListAsync(cancellationToken);

        var totalAmount = todaySales.Sum(s => s.FinalPrice);
        var count = todaySales.Count;

        var response = new TodaySalesResponse(todaySales, totalAmount, count);

        return Result.Success(response);
    }
}