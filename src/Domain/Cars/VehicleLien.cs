using Domain.Shared.ValueObjects;
using SharedKernel;

namespace Domain.Cars;

public enum VehicleLienStatus
{
    Active,
    Liquidated,
}

/// <summary>
/// sfc B7.2 / RN-037: a lien (prenda) registered over a vehicle. While Active the vehicle cannot be transferred: no sale
/// over it can be completed. Liquidating it, once the debt is paid, lifts the block.
/// </summary>
public sealed class VehicleLien : Entity
{
    public Guid CarId { get; private set; }
    public string Creditor { get; private set; } = string.Empty;
    public Money Amount { get; private set; } = null!;
    public string? RegistrationNumber { get; private set; }
    public VehicleLienStatus Status { get; private set; }
    public DateTime RegisteredAtUtc { get; private set; }
    public DateTime? LiquidatedAtUtc { get; private set; }

    private VehicleLien() { }

    public static Result<VehicleLien> Register(
        Guid dealerId, Guid carId, string creditor, Money amount, string? registrationNumber, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(creditor))
            return Result.Failure<VehicleLien>(Error.Validation("VehicleLien.CreditorRequired", "Indicá el acreedor de la prenda."));
        if (amount.Amount <= 0)
            return Result.Failure<VehicleLien>(Error.Validation("VehicleLien.AmountMustBePositive", "El monto de la prenda debe ser mayor a cero."));

        var lien = new VehicleLien
        {
            Id = Guid.NewGuid(),
            CarId = carId,
            Creditor = creditor.Trim(),
            Amount = amount,
            RegistrationNumber = string.IsNullOrWhiteSpace(registrationNumber) ? null : registrationNumber.Trim(),
            Status = VehicleLienStatus.Active,
            RegisteredAtUtc = nowUtc,
        };
        lien.SetDealer(dealerId);
        return lien;
    }

    public Result Liquidate(DateTime nowUtc)
    {
        if (Status == VehicleLienStatus.Liquidated)
            return Result.Failure(Error.Conflict("VehicleLien.AlreadyLiquidated", "La prenda ya fue liquidada."));

        Status = VehicleLienStatus.Liquidated;
        LiquidatedAtUtc = nowUtc;
        return Result.Success();
    }
}

public static class VehicleLienErrors
{
    public static Error ActiveLienBlocksTransfer(Guid carId) => Error.Conflict(
        "ACTIVE_LIEN_BLOCKS_TRANSFER", $"El vehículo '{carId}' tiene una prenda activa.");

    public static Error NotFound(Guid lienId) => Error.NotFound(
        "VehicleLien.NotFound", $"La prenda '{lienId}' no existe.");
}
