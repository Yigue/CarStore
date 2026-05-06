using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Cars.GetAll;

public sealed record GetAllCarsQuery(
    int Page = 1,
    int PageSize = 20) : IQuery<PaginatedResult<CarsResponses>>;
