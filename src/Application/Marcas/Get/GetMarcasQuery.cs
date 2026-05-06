using Application.Abstractions.Messaging;
using Domain.Cars.Attributes;
using SharedKernel;

namespace Application.Marcas.Get;

public sealed record GetMarcasQuery : IQuery<List<Marca>>;
