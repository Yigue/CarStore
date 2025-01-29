using Application.Abstractions.Messaging;

namespace Application.Cars.GetById;

public sealed record GetCarByIdQuery(Guid CarId) : IQuery<CarGetByIdResponse>;
