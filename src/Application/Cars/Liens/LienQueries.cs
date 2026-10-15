using Application.Abstractions.Data;
using Domain.Cars;
using Microsoft.EntityFrameworkCore;

namespace Application.Cars.Liens;

/// <summary>sfc B7.2 / RN-037: the one check every sale-completion path runs before transferring a vehicle.</summary>
internal static class LienQueries
{
    public static Task<bool> HasActiveLienAsync(this IApplicationDbContext context, Guid carId, CancellationToken cancellationToken) =>
        context.VehicleLiens.AnyAsync(l => l.CarId == carId && l.Status == VehicleLienStatus.Active, cancellationToken);
}
