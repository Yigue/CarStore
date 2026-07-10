using Application.DealerSettings.Queries.GetHostName;
using Microsoft.EntityFrameworkCore;
using Moq;
using Application.Abstractions.Tenancy;
using SharedKernel;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.UnitTests.DealerSettings;

/// <summary>
/// TDD tests for GetHostNameQueryHandler (task 1.5.2).
/// RED: written before implementation exists.
/// </summary>
public sealed class GetHostNameQueryHandlerTests
{
    private static readonly Guid TestDealerId = Guid.Parse("bbbb0000-0000-0000-0000-000000000002");

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

    [Fact]
    public async Task Handle_SettingsExist_ReturnsHostNameResponse()
    {
        // Arrange
        using var context = CreateContext();
        var settings = new DealerSettingsEntity(
            dealerId: TestDealerId,
            dealerName: "Test Dealer",
            contactEmail: "test@test.com",
            hostName: "lux.carstore.com",
            slug: "lux");
        context.DealerSettings.Add(settings);
        await context.SaveChangesAsync();

        var tenant = MockTenant(TestDealerId);
        var handler = new GetHostNameQueryHandler(context, tenant.Object);

        // Act
        Result<HostNameResponse> result = await handler.Handle(new GetHostNameQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HostName.Should().Be("lux.carstore.com");
        result.Value.Slug.Should().Be("lux");
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoSettings_ReturnsNotFoundError()
    {
        // Arrange — empty DB
        using var context = CreateContext();
        var tenant = MockTenant(TestDealerId);
        var handler = new GetHostNameQueryHandler(context, tenant.Object);

        // Act
        Result<HostNameResponse> result = await handler.Handle(new GetHostNameQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
