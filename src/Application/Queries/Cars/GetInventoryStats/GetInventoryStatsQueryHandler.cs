using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Queries.Cars.GetInventoryStats;

internal sealed class GetInventoryStatsQueryHandler
    : IQueryHandler<GetInventoryStatsQuery, InventoryStatsResponse>
{
    private readonly IApplicationDbContext _context;

    public GetInventoryStatsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<InventoryStatsResponse>> Handle(
        GetInventoryStatsQuery query,
        CancellationToken cancellationToken)
    {
        var cars = await _context.Cars
            .AsNoTracking()
            .Include(c => c.Marca)
            .ToListAsync(cancellationToken);

        var totalCount = cars.Count;
        // sfc B7.1 / RN-036: consigned vehicles belong to third parties, not to the dealer's own inventory value.
        var totalValue = cars.Where(c => !c.IsConsigned).Sum(c => c.Price.Amount);

        // sfc B6.1 / RN-042: EnVenta is on sale too; it used to fall through every bucket.
        var available = cars.Count(c => c.ServiceCar is StatusServiceCar.Disponible or StatusServiceCar.EnVenta);
        var reserved = cars.Count(c => c.ServiceCar == StatusServiceCar.Reservado);
        var sold = cars.Count(c => c.ServiceCar == StatusServiceCar.Vendido);

        var byBrand = cars
            .GroupBy(c => c.Marca.Nombre)
            .ToDictionary(g => g.Key, g => g.Count());

        var byType = cars
            .GroupBy(c => c.CarType.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var response = new InventoryStatsResponse(
            totalCount,
            totalValue,
            new InventoryByStatus(available, reserved, sold),
            byBrand,
            byType);

        return Result.Success(response);
    }
}