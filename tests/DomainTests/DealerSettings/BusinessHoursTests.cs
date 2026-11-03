using Domain.DealerSettings;
using SharedKernel;

namespace DomainTests.DealerSettings;

/// <summary>
/// sfc B5.2 / RN-044: the weekly schedule the public test-drive slot query reads from. Same
/// validate-everything-before-mutating posture as <see cref="UpdateLandingTests"/>.
/// </summary>
public class BusinessHoursTests
{
    private static Domain.DealerSettings.DealerSettings CreateDealer()
        => new(
            dealerId: Guid.NewGuid(),
            dealerName: "Test Dealer",
            contactEmail: "test@dealer.com");

    [Fact]
    public void GetBusinessHours_IsEmpty_BeforeAnythingIsConfigured()
    {
        var dealer = CreateDealer();

        dealer.GetBusinessHours().Should().BeEmpty();
    }

    [Fact]
    public void SetBusinessHours_StoresTheRetrievableEntries()
    {
        var dealer = CreateDealer();
        var entries = new List<BusinessHoursEntry>
        {
            new(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(18, 0)),
            new(DayOfWeek.Saturday, new TimeOnly(9, 0), new TimeOnly(13, 0)),
        };

        dealer.SetBusinessHours(entries);

        dealer.GetBusinessHours().Should().BeEquivalentTo(entries);
    }

    [Fact]
    public void SetBusinessHours_AllowsAnExplicitlyClosedDay_WithBothTimesNull()
    {
        var dealer = CreateDealer();
        var entries = new List<BusinessHoursEntry> { new(DayOfWeek.Sunday, null, null) };

        dealer.SetBusinessHours(entries);

        dealer.GetBusinessHours().Single().OpenTime.Should().BeNull();
    }

    [Fact]
    public void SetBusinessHours_NullOrEmpty_ClearsTheSchedule()
    {
        var dealer = CreateDealer();
        dealer.SetBusinessHours([new BusinessHoursEntry(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(18, 0))]);

        dealer.SetBusinessHours(null);

        dealer.GetBusinessHours().Should().BeEmpty();
    }

    [Fact]
    public void SetBusinessHours_Fails_WhenTheSameDayIsListedTwice()
    {
        var dealer = CreateDealer();
        var entries = new List<BusinessHoursEntry>
        {
            new(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0)),
            new(DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(18, 0)),
        };

        var act = () => dealer.SetBusinessHours(entries);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SetBusinessHours_Fails_WhenOpenTimeIsNotEarlierThanCloseTime()
    {
        var dealer = CreateDealer();
        var entries = new List<BusinessHoursEntry> { new(DayOfWeek.Monday, new TimeOnly(18, 0), new TimeOnly(9, 0)) };

        var act = () => dealer.SetBusinessHours(entries);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SetBusinessHours_Fails_WhenOnlyOneOfOpenCloseIsSet()
    {
        var dealer = CreateDealer();
        var entries = new List<BusinessHoursEntry> { new(DayOfWeek.Monday, new TimeOnly(9, 0), null) };

        var act = () => dealer.SetBusinessHours(entries);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SetBusinessHours_Fails_WhenMoreThanSevenEntries()
    {
        var dealer = CreateDealer();
        var entries = Enum.GetValues<DayOfWeek>()
            .Select(d => new BusinessHoursEntry(d, new TimeOnly(9, 0), new TimeOnly(18, 0)))
            .Append(new BusinessHoursEntry(DayOfWeek.Monday, new TimeOnly(19, 0), new TimeOnly(20, 0)))
            .ToList();

        var act = () => dealer.SetBusinessHours(entries);

        act.Should().Throw<DomainException>();
    }
}
