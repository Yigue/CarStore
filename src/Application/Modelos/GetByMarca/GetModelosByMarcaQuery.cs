using Application.Abstractions.Messaging;
using Domain.Cars.Attributes;
using SharedKernel;

namespace Application.Modelos.GetByMarca;

public sealed record GetModelosByMarcaQuery(Guid MarcaId) : IQuery<List<Modelo>>;
