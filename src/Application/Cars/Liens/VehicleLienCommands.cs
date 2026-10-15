using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Cars;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.Liens;

/// <summary>sfc B7.2 / RN-037: registers a lien over one of the caller's vehicles. Returns the lien id.</summary>
public sealed record RegisterVehicleLienCommand(
    Guid CarId, string Creditor, decimal Amount, string Currency, string? RegistrationNumber) : ICommand<Guid>;

/// <summary>sfc B7.2 / RN-037: marks the debt as paid; the vehicle can be sold again.</summary>
public sealed record LiquidateVehicleLienCommand(Guid LienId) : ICommand;

/// <summary>sfc B7.2: the liens of one vehicle, newest first.</summary>
public sealed record GetVehicleLiensQuery(Guid CarId) : IQuery<IReadOnlyList<VehicleLienResponse>>;

public sealed record VehicleLienResponse(
    Guid Id, Guid CarId, string Creditor, decimal Amount, string Currency, string? RegistrationNumber,
    string Status, DateTime RegisteredAtUtc, DateTime? LiquidatedAtUtc);

internal sealed class RegisterVehicleLienCommandHandler(
    IApplicationDbContext context, IDateTimeProvider dateTimeProvider, ICurrentTenantService tenantService)
    : ICommandHandler<RegisterVehicleLienCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterVehicleLienCommand command, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return Result.Failure<Guid>(guard.Error);

        Guid dealerId = tenantService.DealerId;
        bool ownsCar = await context.Cars.AnyAsync(c => c.Id == command.CarId && c.DealerId == dealerId, cancellationToken);
        if (!ownsCar)
            return Result.Failure<Guid>(CarErrors.NotFound(command.CarId));

        if (!CurrencyCode.TryParse(command.Currency, out CurrencyCode currency))
            return Result.Failure<Guid>(Error.Validation("Currency.Unsupported", "Unsupported currency. Supported: ARS, USD."));

        Result<VehicleLien> lien = VehicleLien.Register(
            dealerId, command.CarId, command.Creditor, Money.Of(command.Amount, currency), command.RegistrationNumber,
            dateTimeProvider.UtcNow);
        if (lien.IsFailure)
            return Result.Failure<Guid>(lien.Error);

        context.VehicleLiens.Add(lien.Value);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(lien.Value.Id);
    }
}

internal sealed class LiquidateVehicleLienCommandHandler(
    IApplicationDbContext context, IDateTimeProvider dateTimeProvider, ICurrentTenantService tenantService)
    : ICommandHandler<LiquidateVehicleLienCommand>
{
    public async Task<Result> Handle(LiquidateVehicleLienCommand command, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return guard;

        VehicleLien? lien = await context.VehicleLiens.FirstOrDefaultAsync(l => l.Id == command.LienId, cancellationToken);
        if (lien is null || lien.DealerId != tenantService.DealerId)
            return Result.Failure(VehicleLienErrors.NotFound(command.LienId));

        Result liquidated = lien.Liquidate(dateTimeProvider.UtcNow);
        if (liquidated.IsFailure)
            return liquidated;

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class GetVehicleLiensQueryHandler(IApplicationDbContext context, ICurrentTenantService tenantService)
    : IQueryHandler<GetVehicleLiensQuery, IReadOnlyList<VehicleLienResponse>>
{
    public async Task<Result<IReadOnlyList<VehicleLienResponse>>> Handle(GetVehicleLiensQuery query, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return Result.Failure<IReadOnlyList<VehicleLienResponse>>(guard.Error);

        Guid dealerId = tenantService.DealerId;
        IReadOnlyList<VehicleLienResponse> liens = await context.VehicleLiens
            .AsNoTracking()
            .Where(l => l.DealerId == dealerId && l.CarId == query.CarId)
            .OrderByDescending(l => l.RegisteredAtUtc)
            .Select(l => new VehicleLienResponse(
                l.Id, l.CarId, l.Creditor, l.Amount.Amount, l.Amount.Currency, l.RegistrationNumber,
                l.Status.ToString(), l.RegisteredAtUtc, l.LiquidatedAtUtc))
            .ToListAsync(cancellationToken);
        return Result.Success(liens);
    }
}
