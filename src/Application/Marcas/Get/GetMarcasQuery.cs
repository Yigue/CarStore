using Application.Abstractions.Messaging;
using Domain.Cars.Atribbutes;
using SharedKernel;

namespace Application.Marcas.Get;

public sealed record GetMarcasQuery : IQuery<List<Marca>>;
