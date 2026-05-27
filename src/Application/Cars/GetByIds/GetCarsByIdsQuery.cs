using Application.Abstractions.Messaging;
using Application.Cars.Get;

namespace Application.Cars.GetByIds;

public sealed record GetCarsByIdsQuery(List<Guid> Ids) : IQuery<List<CarResponse>>;
