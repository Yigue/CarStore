using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Cars;
using Domain.Currency;
using Domain.Sales.Attributes;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.Kpis;

/// <summary>
/// sfc B6.1 / RN-042. Costs are brought to the sale currency with the sale's own exchange-rate snapshot; when that is not
/// possible the margin is left null instead of adding amounts in different currencies.
/// </summary>
internal sealed class GetInventoryKpisQueryHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetInventoryKpisQuery, IReadOnlyList<InventoryKpiResponse>>
{
    private sealed record SoldRow(Guid CarId, DateTime SaleDate, Money FinalPrice, ExchangeRateSnapshot? ExchangeRate);

    public async Task<Result<IReadOnlyList<InventoryKpiResponse>>> Handle(GetInventoryKpisQuery query, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return Result.Failure<IReadOnlyList<InventoryKpiResponse>>(guard.Error);

        Guid dealerId = tenantService.DealerId;
        DateTime today = dateTimeProvider.UtcNow.Date;

        List<Car> cars = await context.Cars
            .AsNoTracking()
            .Include(c => c.ReconditioningTasks)
            .Where(c => c.DealerId == dealerId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        List<SoldRow> sold = await context.Sales
            .AsNoTracking()
            .Where(s => s.DealerId == dealerId && s.Status == SaleStatus.Completed)
            .Select(s => new SoldRow(s.CarId, s.SaleDate, s.FinalPrice, s.ExchangeRate))
            .ToListAsync(cancellationToken);
        Dictionary<Guid, SoldRow> saleByCar = sold
            .GroupBy(s => s.CarId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.SaleDate).First());

        var rows = cars.Select(car => Kpi(car, saleByCar.GetValueOrDefault(car.Id), today)).ToList();
        return Result.Success<IReadOnlyList<InventoryKpiResponse>>(rows);
    }

    private static InventoryKpiResponse Kpi(Car car, SoldRow? sale, DateTime today)
    {
        DateTime end = (sale?.SaleDate ?? today).Date;
        int daysInStock = Math.Max(0, (end - car.CreatedAt.Date).Days);

        CurrencyCode currency = CurrencyCode.Parse(sale?.FinalPrice.Currency ?? car.Price.Currency);
        decimal? purchase = car.PurchaseCost is null ? 0m : ToCurrency(car.PurchaseCost, currency, sale?.ExchangeRate);
        decimal? reconditioning = 0m;
        foreach (ReconditioningTask task in car.ReconditioningTasks.Where(t => t.Status == ReconditioningStatus.Completed))
        {
            decimal? cost = ToCurrency(task.Cost, currency, sale?.ExchangeRate);
            reconditioning = cost is null || reconditioning is null ? null : reconditioning + cost;
        }

        decimal? margin = sale is null || purchase is null || reconditioning is null
            ? null
            : sale.FinalPrice.Amount - purchase.Value - reconditioning.Value;

        return new InventoryKpiResponse(
            car.Id,
            car.Patente.Value,
            car.ServiceCar.ToString(),
            car.CreatedAt,
            sale?.SaleDate,
            daysInStock,
            currency.Value,
            sale?.FinalPrice.Amount,
            car.PurchaseCost is null ? null : purchase,
            reconditioning,
            margin);
    }

    private static decimal? ToCurrency(Money amount, CurrencyCode target, ExchangeRateSnapshot? snapshot)
    {
        if (amount.Currency == target.Value)
            return amount.Amount;

        bool convertible = snapshot is not null
                           && ((snapshot.Base == amount.Currency && snapshot.Quote == target.Value)
                               || (snapshot.Quote == amount.Currency && snapshot.Base == target.Value));
        return convertible ? snapshot!.Convert(amount, target).Amount : null;
    }
}
