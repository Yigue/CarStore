using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Marcas.Get;

public sealed record GetMarcasQuery : IQuery<List<MarcaCacheDto>>;
