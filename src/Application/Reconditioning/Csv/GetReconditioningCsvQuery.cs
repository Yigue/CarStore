using System.Globalization;
using System.Text;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reconditioning.Csv;

/// <summary>sfc B7.3: the car's reconditioning tasks as CSV (description, cost, currency, status).</summary>
public sealed record GetReconditioningCsvQuery(Guid CarId) : IQuery<string>;

internal sealed class GetReconditioningCsvQueryHandler(IApplicationDbContext context, ICurrentTenantService tenantService)
    : IQueryHandler<GetReconditioningCsvQuery, string>
{
    public async Task<Result<string>> Handle(GetReconditioningCsvQuery query, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return Result.Failure<string>(guard.Error);

        Car? car = await context.Cars.AsNoTracking()
            .Include(c => c.ReconditioningTasks)
            .FirstOrDefaultAsync(c => c.Id == query.CarId, cancellationToken);
        if (car is null || car.DealerId != tenantService.DealerId)
            return Result.Failure<string>(CarErrors.NotFound(query.CarId));

        var csv = new StringBuilder("description,cost,currency,status\n");
        foreach (ReconditioningTask task in car.ReconditioningTasks)
        {
            csv.Append(SimpleCsv.Quote(task.Description)).Append(',')
                .Append(task.Cost.Amount.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(task.Cost.Currency).Append(',')
                .Append(task.Status).Append('\n');
        }

        return Result.Success(csv.ToString());
    }
}
