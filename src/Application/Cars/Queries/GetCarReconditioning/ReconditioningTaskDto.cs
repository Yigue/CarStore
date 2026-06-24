using Domain.Cars;

namespace Application.Cars.Queries.GetCarReconditioning;

public sealed record ReconditioningTaskDto(
    Guid Id,
    string Description,
    decimal Cost,
    string Currency,
    ReconditioningStatus Status,
    DateTime? CompletedAt);

public sealed record GetCarReconditioningResponse(
    IReadOnlyList<ReconditioningTaskDto> Tasks,
    decimal TotalCostOfOwnership,
    string Currency);
