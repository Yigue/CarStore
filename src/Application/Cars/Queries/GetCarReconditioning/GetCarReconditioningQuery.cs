using Application.Abstractions.Messaging;

namespace Application.Cars.Queries.GetCarReconditioning;

public sealed record GetCarReconditioningQuery(Guid CarId)
    : IQuery<GetCarReconditioningResponse>;
