using Application.Abstractions.Messaging;
using Domain.Cars.Attributes;

namespace Application.Modelos.Get;

public sealed record GetModelosQuery() : IQuery<List<Application.Abstractions.Caching.ModeloCacheDto>>;
