using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Cars.GetAll;

/// <summary>
/// <paramref name="SortBy"/> and <paramref name="SortOrder"/> are validated against
/// <see cref="CarSortOrder"/>; an unsupported value fails the query with a validation
/// error (HTTP 400) rather than being ignored. Both null means newest first.
/// </summary>
public sealed record GetAllCarsQuery(
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    string? SortOrder = null) : IQuery<PaginatedResult<CarsResponses>>;
