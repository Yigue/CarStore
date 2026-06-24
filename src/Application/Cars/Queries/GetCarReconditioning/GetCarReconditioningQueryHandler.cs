using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.Queries.GetCarReconditioning;

internal sealed class GetCarReconditioningQueryHandler(
    IApplicationDbContext context)
    : IQueryHandler<GetCarReconditioningQuery, GetCarReconditioningResponse>
{
    public async Task<Result<GetCarReconditioningResponse>> Handle(
        GetCarReconditioningQuery query,
        CancellationToken cancellationToken)
    {
        // We need behavior (GetTotalCostOfOwnership) so we load the aggregate.
        // AsNoTracking keeps it cheap because we never mutate.
        Car? car = await context.Cars
            .AsNoTracking()
            .Include(c => c.ReconditioningTasks)
            .SingleOrDefaultAsync(c => c.Id == query.CarId, cancellationToken);

        if (car is null)
        {
            return Result.Failure<GetCarReconditioningResponse>(CarErrors.NotFound(query.CarId));
        }

        IReadOnlyList<ReconditioningTaskDto> tasks = car.ReconditioningTasks
            .Select(t => new ReconditioningTaskDto(
                t.Id,
                t.Description,
                t.Cost.Amount,
                t.Cost.Currency,
                t.Status,
                t.CompletedAt))
            .ToList();

        Domain.Shared.ValueObjects.Money tco = car.GetTotalCostOfOwnership();

        var response = new GetCarReconditioningResponse(
            tasks,
            tco.Amount,
            tco.Currency);

        return Result.Success(response);
    }
}
