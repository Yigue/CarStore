using Domain.Cars;
using SharedKernel;

namespace Application.Cars.GetAll;

/// <summary>
/// Whitelist of sort fields accepted by <c>GET /cars</c>, plus the ordering they map to.
///
/// The endpoint used to bind neither <c>sortBy</c> nor <c>sortOrder</c>, so any value —
/// including a field that does not exist — was silently dropped and every request came
/// back in the same (unspecified) order. Callers had no way to tell a working sort from
/// a typo. Anything outside this whitelist is now rejected with 400 instead.
/// </summary>
internal static class CarSortOrder
{
    /// <summary>Applied when the caller does not ask for a sort: newest inventory first.</summary>
    private const string DefaultField = "createdat";

    private const string DefaultDirection = "desc";

    /// <summary>
    /// Canonical field names, plus the aliases the dashboard and public catalog already
    /// use for the same column. Keys are lowercase; lookup lowercases the input so the
    /// contract is case-insensitive.
    /// </summary>
    private static readonly Dictionary<string, string> FieldAliases = new(StringComparer.Ordinal)
    {
        ["price"] = "price",
        ["precio"] = "price",
        ["year"] = "year",
        ["anio"] = "year",
        ["kilometraje"] = "kilometraje",
        ["mileage"] = "kilometraje",
        ["fueltype"] = "fueltype",
        ["marca"] = "marca",
        ["modelo"] = "modelo",
        ["status"] = "status",
        ["servicestatus"] = "servicestatus",
        ["createdat"] = "createdat",
        ["created"] = "createdat",
        ["updatedat"] = "updatedat",
    };

    private static readonly HashSet<string> Directions = new(StringComparer.Ordinal) { "asc", "desc" };

    public static string SupportedFields =>
        string.Join(", ", FieldAliases.Keys.OrderBy(k => k, StringComparer.Ordinal));

    /// <summary>
    /// Validates the requested sort and applies it to <paramref name="query"/>.
    /// Ordering is applied to the full query so paging always slices an already-sorted
    /// set — sorting after Skip/Take would only order the rows of the current page.
    /// </summary>
    public static Result<IOrderedQueryable<Car>> Apply(IQueryable<Car> query, string? sortBy, string? sortOrder)
    {
        string requestedField = string.IsNullOrWhiteSpace(sortBy) ? DefaultField : sortBy.Trim().ToLowerInvariant();

        if (!FieldAliases.TryGetValue(requestedField, out string? field))
        {
            return Result.Failure<IOrderedQueryable<Car>>(Error.Validation(
                "Cars.InvalidSortField",
                $"'{sortBy}' is not a sortable field. Supported fields: {SupportedFields}."));
        }

        string direction = string.IsNullOrWhiteSpace(sortOrder)
            ? DefaultDirection
            : sortOrder.Trim().ToLowerInvariant();

        if (!Directions.Contains(direction))
        {
            return Result.Failure<IOrderedQueryable<Car>>(Error.Validation(
                "Cars.InvalidSortOrder",
                $"'{sortOrder}' is not a valid sort order. Supported values: asc, desc."));
        }

        bool descending = direction == "desc";

        IOrderedQueryable<Car> ordered = field switch
        {
            // Price is a Money value object behind a ValueConverter over a single decimal
            // column, so the property itself (not Price.Amount, which does not translate)
            // is what relational providers turn into ORDER BY price.
            "price" => Order(query, c => c.Price, descending),
            "year" => Order(query, c => c.Anio, descending),
            "kilometraje" => Order(query, c => c.Kilometraje, descending),
            "fueltype" => Order(query, c => c.FuelType, descending),
            "marca" => Order(query, c => c.Marca.Nombre, descending),
            "modelo" => Order(query, c => c.Modelo.Nombre, descending),
            "status" => Order(query, c => c.CarStatus, descending),
            "servicestatus" => Order(query, c => c.ServiceCar, descending),
            "updatedat" => Order(query, c => c.UpdatedAt, descending),
            _ => Order(query, c => c.CreatedAt, descending),
        };

        // Ids break ties so paging stays stable across requests when the sort key repeats.
        return Result.Success(ordered.ThenBy(c => c.Id));
    }

    private static IOrderedQueryable<Car> Order<TKey>(
        IQueryable<Car> query,
        System.Linq.Expressions.Expression<Func<Car, TKey>> selector,
        bool descending) =>
        descending ? query.OrderByDescending(selector) : query.OrderBy(selector);
}
