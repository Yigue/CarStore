using Domain.AfterSales;
using FluentAssertions;

namespace DomainTests.AfterSales;

/// <summary>sfc C2.3 / RN-049: a warranty runs from the delivery date for its months and is then over.</summary>
public class WarrantyRecordTests
{
    private static readonly DateTime Delivered = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_warranty_covers_from_delivery_for_its_months_only()
    {
        var warranty = WarrantyRecord.Register(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Delivered, 12, "Motor y caja").Value;

        warranty.EndsOnUtc.Should().Be(Delivered.AddMonths(12));
        warranty.IsActiveOn(Delivered.AddMonths(11)).Should().BeTrue();
        warranty.IsActiveOn(Delivered.AddMonths(12)).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void The_duration_must_be_between_1_and_120_months(int months)
    {
        WarrantyRecord.Register(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Delivered, months, null)
            .IsFailure.Should().BeTrue();
    }
}
