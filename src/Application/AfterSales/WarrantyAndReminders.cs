using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.AfterSales;
using Domain.Sales;
using Domain.Sales.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.AfterSales;

/// <summary>sfc C2.3 / RN-049: registers the warranty of a completed, delivered sale of the caller's dealer.</summary>
public sealed record RegisterWarrantyCommand(Guid SaleId, int Months, string? Conditions) : ICommand<Guid>;

/// <summary>sfc C2.4: the caller's reminders due up to a date, oldest first.</summary>
public sealed record GetServiceRemindersQuery(DateTime? DueUntilUtc = null) : IQuery<IReadOnlyList<ServiceReminderResponse>>;

public sealed record ServiceReminderResponse(Guid Id, Guid SaleId, Guid ClientId, string Kind, DateTime DueOnUtc, bool ClientContactAllowed);

internal sealed class RegisterWarrantyCommandHandler(IApplicationDbContext context, ICurrentTenantService tenantService)
    : ICommandHandler<RegisterWarrantyCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterWarrantyCommand command, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return Result.Failure<Guid>(guard.Error);

        Sale? sale = await context.Sales.FirstOrDefaultAsync(s => s.Id == command.SaleId, cancellationToken);
        if (sale is null || sale.DealerId != tenantService.DealerId)
            return Result.Failure<Guid>(Error.NotFound("Sales.NotFound", $"La venta '{command.SaleId}' no existe."));
        if (sale.Status != SaleStatus.Completed || sale.DeliveryDate is not { } delivered)
            return Result.Failure<Guid>(Error.Conflict("Warranty.SaleNotDelivered", "La garantía se registra sobre una venta completada y entregada."));
        if (await context.WarrantyRecords.AnyAsync(w => w.SaleId == sale.Id, cancellationToken))
            return Result.Failure<Guid>(Error.Conflict("Warranty.AlreadyRegistered", "La venta ya tiene una garantía registrada."));

        Result<WarrantyRecord> warranty = WarrantyRecord.Register(
            sale.DealerId, sale.Id, sale.CarId, sale.ClientId, delivered, command.Months, command.Conditions);
        if (warranty.IsFailure)
            return Result.Failure<Guid>(warranty.Error);

        context.WarrantyRecords.Add(warranty.Value);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(warranty.Value.Id);
    }
}

internal sealed class GetServiceRemindersQueryHandler(
    IApplicationDbContext context, ICurrentTenantService tenantService, IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetServiceRemindersQuery, IReadOnlyList<ServiceReminderResponse>>
{
    public async Task<Result<IReadOnlyList<ServiceReminderResponse>>> Handle(GetServiceRemindersQuery query, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return Result.Failure<IReadOnlyList<ServiceReminderResponse>>(guard.Error);

        Guid dealerId = tenantService.DealerId;
        DateTime until = query.DueUntilUtc ?? dateTimeProvider.UtcNow.AddDays(30);
        IReadOnlyList<ServiceReminderResponse> rows = await context.ServiceReminders.AsNoTracking()
            .Where(r => r.DealerId == dealerId && r.DueOnUtc <= until)
            .OrderBy(r => r.DueOnUtc)
            .Take(500)
            .Select(r => new ServiceReminderResponse(r.Id, r.SaleId, r.ClientId, r.Kind.ToString(), r.DueOnUtc, r.ClientContactAllowed))
            .ToListAsync(cancellationToken);
        return Result.Success(rows);
    }
}
