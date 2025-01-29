using Application.Abstractions.Messaging;

namespace Application.Cars.GetAll;

public sealed record GetAllCarsQuery() : IQuery<List<CarsResponses>>;
