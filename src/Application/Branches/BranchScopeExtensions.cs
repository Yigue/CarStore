using Domain.Cars;

namespace Application.Branches;

/// <summary>sfc C2.2 / ADR C26: applies the branch scope to vehicle listings; null leaves the query untouched.</summary>
public static class BranchScopeExtensions
{
    public static IQueryable<Car> ForBranchScope(this IQueryable<Car> cars, IReadOnlyCollection<Guid>? restrictedTo) =>
        restrictedTo is null
            ? cars
            : cars.Where(c => c.BranchId != null && restrictedTo.Contains(c.BranchId.Value));
}
