using Application.DealerSettings.Commands.UpdateHostName;
using Application.DealerSettings;
using Microsoft.EntityFrameworkCore;
using Moq;
using Application.Abstractions.Tenancy;
using SharedKernel;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.UnitTests.DealerSettings;

/// <summary>
/// TDD tests for UpdateHostNameCommandHandler (task 1.5.1).
/// RED: written before implementation exists.
/// </summary>
public sealed class UpdateHostNameCommandHandlerTests
{
    private static readonly Guid TestDealerId = Guid.Parse("aaaa0000-0000-0000-0000-000000000001");

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options, TestDealerId);
    }

    private static Mock<ICurrentTenantService> MockTenant(Guid dealerId)
    {
        var mock = new Mock<ICurrentTenantService>();
        mock.Setup(t => t.DealerId).Returns(dealerId);
        mock.Setup(t => t.HasTenant).Returns(true);
        return mock;
    }

    private static DealerSettingsEntity SeedSettings(TestApplicationDbContext context)
    {
        var settings = new DealerSettingsEntity(
            dealerId: TestDealerId,
            dealerName: "Test Dealer",
            contactEmail: "test@test.com");
        context.DealerSettings.Add(settings);
        context.SaveChanges();
        return settings;
    }

    [Fact]
    public async Task Handle_ValidSlugAndHostName_UpdatesAndReturnsResponse()
    {
        // Arrange
        using var context = CreateContext();
        SeedSettings(context);
        var tenant = MockTenant(TestDealerId);
        var handler = new UpdateHostNameCommandHandler(context, tenant.Object);

        var command = new UpdateHostNameCommand("lux", "lux.carstore.com");

        // Act
        Result<DealerSettingsResponse> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HostName.Should().Be("lux.carstore.com");
    }

    [Fact]
    public async Task Handle_SettingsNotFound_ReturnsNotFoundError()
    {
        // Arrange — empty DB, no settings for this tenant
        using var context = CreateContext();
        var tenant = MockTenant(TestDealerId);
        var handler = new UpdateHostNameCommandHandler(context, tenant.Object);

        var command = new UpdateHostNameCommand("lux", "lux.carstore.com");

        // Act
        Result<DealerSettingsResponse> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
