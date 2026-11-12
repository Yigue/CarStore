using SharedKernel;

namespace Domain.AfterSales;

/// <summary>sfc C2.3 / RN-049: the warranty of a sale, running from its delivery date for a number of months.</summary>
public sealed class WarrantyRecord : Entity
{
    public const int MaxMonths = 120;

    public Guid SaleId { get; private set; }
    public Guid CarId { get; private set; }
    public Guid ClientId { get; private set; }
    public DateTime StartsOnUtc { get; private set; }
    public int Months { get; private set; }
    public DateTime EndsOnUtc { get; private set; }
    public string? Conditions { get; private set; }

    private WarrantyRecord() { }

    public static Result<WarrantyRecord> Register(
        Guid dealerId, Guid saleId, Guid carId, Guid clientId, DateTime startsOnUtc, int months, string? conditions)
    {
        if (months < 1 || months > MaxMonths)
            return Result.Failure<WarrantyRecord>(Error.Validation("Warranty.InvalidMonths", $"La garantía debe durar entre 1 y {MaxMonths} meses."));

        var warranty = new WarrantyRecord
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            CarId = carId,
            ClientId = clientId,
            StartsOnUtc = startsOnUtc,
            Months = months,
            EndsOnUtc = startsOnUtc.AddMonths(months),
            Conditions = string.IsNullOrWhiteSpace(conditions) ? null : conditions.Trim(),
        };
        warranty.SetDealer(dealerId);
        return warranty;
    }

    public bool IsActiveOn(DateTime dateUtc) => dateUtc >= StartsOnUtc && dateUtc < EndsOnUtc;
}
