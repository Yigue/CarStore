using Application.Abstractions.Authentication;
using Application.Abstractions.Billing;
using Application.Dealers.Provision;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ApplicationTests.Dealers.Provision;

/// <summary>sfc A4.3 / C17: a new dealer never exists without an explicit owner; the first administrator is it.</summary>
public class ProvisionDealerOwnerTests
{
    [Fact]
    public async Task Handle_SetsDealerSettingsOwnerUserId_ToTheFirstAdministrator()
    {
        var options = new DbContextOptionsBuilder<Application.UnitTests.TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new Application.UnitTests.TestApplicationDbContext(options);
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");
        var publisher = new Mock<MediatR.IPublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<MediatR.INotification>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var gateway = new Mock<ISubscriptionGateway>();
        var handler = new ProvisionDealerCommandHandler(
            context, context, hasher.Object, publisher.Object, gateway.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProvisionDealerCommandHandler>.Instance,
            Application.UnitTests.Legal.FakeLegalTermsProvider.Recorder(context));

        var result = await handler.Handle(
            new ProvisionDealerCommand(
                "Automotors del Sur", "automotors", "admin@automotors.com", "Sup3r$ecret!", "Ana", "García",
                Application.UnitTests.Legal.FakeLegalTermsProvider.CurrentVersion),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var settings = await context.DealerSettings.IgnoreQueryFilters().SingleAsync(s => s.DealerId == result.Value.DealerId);
        settings.OwnerUserId.Should().Be(result.Value.AdminUserId);
        var admin = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == result.Value.AdminUserId);
        admin.Email.Value.Should().Be("admin@automotors.com");
    }
}
