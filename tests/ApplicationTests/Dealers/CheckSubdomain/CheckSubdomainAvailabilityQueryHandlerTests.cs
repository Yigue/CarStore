using Application.Common;
using Application.Dealers.CheckSubdomain;
using Microsoft.EntityFrameworkCore;

namespace ApplicationTests.Dealers.CheckSubdomain;

public class CheckSubdomainAvailabilityQueryHandlerTests
{
    private static Application.UnitTests.TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<Application.UnitTests.TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new Application.UnitTests.TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_ShouldReturnAvailable_WhenSubdomainUnused()
    {
        using var context = CreateContext();
        var handler = new CheckSubdomainAvailabilityQueryHandler(context);

        var result = await handler.Handle(
            new CheckSubdomainAvailabilityQuery("automotors"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Available.Should().BeTrue();
        result.Value.Reserved.Should().BeFalse();
        result.Value.Reason.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnNotAvailable_WhenSubdomainAlreadyTaken()
    {
        using var context = CreateContext();

        var dealerId = Guid.NewGuid();
        var settings = new Domain.DealerSettings.DealerSettings(
            dealerId,
            "Existing Dealer",
            "owner@existing.com",
            notificationsEnabled: true,
            hostName: "taken");
        context.DealerSettings.Add(settings);
        await context.SaveChangesAsync();

        var handler = new CheckSubdomainAvailabilityQueryHandler(context);
        var result = await handler.Handle(
            new CheckSubdomainAvailabilityQuery("taken"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Available.Should().BeFalse();
        result.Value.Reserved.Should().BeFalse();
        result.Value.Reason.Should().Be("taken");
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("api")]
    [InlineData("www")]
    [InlineData("dashboard")]
    [InlineData("internal")]
    public async Task Handle_ShouldReturnReserved_WhenSlugInBlocklist(string reserved)
    {
        using var context = CreateContext();
        var handler = new CheckSubdomainAvailabilityQueryHandler(context);

        var result = await handler.Handle(
            new CheckSubdomainAvailabilityQuery(reserved),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Available.Should().BeFalse();
        result.Value.Reserved.Should().BeTrue();
        result.Value.Reason.Should().Be("reserved");
    }

    [Fact]
    public async Task Handle_ShouldMatchCaseInsensitive_OnReservedList()
    {
        using var context = CreateContext();
        var handler = new CheckSubdomainAvailabilityQueryHandler(context);

        var result = await handler.Handle(
            new CheckSubdomainAvailabilityQuery("ADMIN"),
            CancellationToken.None);

        result.Value.Reserved.Should().BeTrue();
        result.Value.Available.Should().BeFalse();
    }
}