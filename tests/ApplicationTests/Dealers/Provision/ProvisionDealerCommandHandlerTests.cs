using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Application.Common;
using Application.Dealers.Provision;
using Domain.DealerSettings;
using Domain.DealerSettings.Events;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace ApplicationTests.Dealers.Provision;

public class ProvisionDealerCommandHandlerTests
{
    private static Application.UnitTests.TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<Application.UnitTests.TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new Application.UnitTests.TestApplicationDbContext(options);
    }

    private static Mock<IPasswordHasher> HasherReturning(string hash = "hashed-pw")
    {
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns(hash);
        return hasher;
    }

    private static ProvisionDealerCommand ValidCommand(string subdomain = "automotors") => new(
        DealerName: "Automotors del Sur",
        Subdomain: subdomain,
        AdminEmail: "admin@automotors.com",
        AdminPassword: "Sup3r$ecret!",
        AdminFirstName: "Ana",
        AdminLastName: "García");

    [Fact]
    public async Task Handle_ShouldCreateDealerSettingsAndAdminUser_OnSuccess()
    {
        using var context = CreateContext();
        var hasher = HasherReturning();
        var publisher = new Mock<MediatR.IPublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<MediatR.INotification>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var handler = new ProvisionDealerCommandHandler(context, context, hasher.Object, publisher.Object);
        var command = ValidCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DealerId.Should().NotBe(Guid.Empty);
        result.Value.AdminUserId.Should().NotBe(Guid.Empty);
        result.Value.Subdomain.Should().Be("automotors");

        var settings = await context.DealerSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.DealerId == result.Value.DealerId);
        settings.Should().NotBeNull();
        settings!.HostName.Should().Be("automotors");
        settings.DealerName.Should().Be("Automotors del Sur");

        var user = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == result.Value.AdminUserId);
        user.Should().NotBeNull();
        user!.Role.Should().Be(UserRole.Admin);
        user.DealerId.Should().Be(result.Value.DealerId);
        user.Email.Value.Should().Be("admin@automotors.com");
    }

    [Fact]
    public async Task Handle_ShouldUseSameGuid_ForDealerSettingsIdAndDealerId()
    {
        using var context = CreateContext();
        var publisher = new Mock<MediatR.IPublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<MediatR.INotification>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        var handler = new ProvisionDealerCommandHandler(context, context, HasherReturning().Object, publisher.Object);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var settings = await context.DealerSettings
            .IgnoreQueryFilters()
            .FirstAsync(s => s.DealerId == result.Value.DealerId);
        settings.Id.Should().Be(result.Value.DealerId,
            "Per design ADR-1 the row PK (Id) and the tenant FK (DealerId) must share the same Guid in the provision path.");
    }

    [Fact]
    public async Task Handle_ShouldRollBack_WhenUserWriteFails()
    {
        // The hasher throws inside the User ctor. The transaction's `using` block MUST
        // roll back, leaving zero DealerSettings rows committed.
        using var context = CreateContext();

        var publisher = new Mock<MediatR.IPublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<MediatR.INotification>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Throws(new InvalidOperationException("hash failed"));

        var handler = new ProvisionDealerCommandHandler(context, context, hasher.Object, publisher.Object);

        var act = async () => await handler.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();

        var settingsCount = await context.DealerSettings.IgnoreQueryFilters().CountAsync();
        settingsCount.Should().Be(0,
            "if the User write fails the transaction MUST roll back and leave zero DealerSettings rows");
    }

    [Fact]
    public async Task Handle_ShouldPublishDealerProvisionedDomainEvent_ExactlyOnce_OnSuccess()
    {
        using var context = CreateContext();
        var publisher = new Mock<MediatR.IPublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<MediatR.INotification>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        var handler = new ProvisionDealerCommandHandler(context, context, HasherReturning().Object, publisher.Object);

        var result = await handler.Handle(ValidCommand("acme"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        publisher.Verify(
            p => p.Publish(It.Is<DealerProvisionedDomainEvent>(e =>
                e.DealerId == result.Value.DealerId &&
                e.AdminUserId == result.Value.AdminUserId &&
                e.Subdomain == "acme" &&
                e.DashboardUrl.Contains("acme.carstore.com")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotPublishEvent_WhenTransactionFails()
    {
        using var context = CreateContext();
        var publisher = new Mock<MediatR.IPublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<MediatR.INotification>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Throws(new InvalidOperationException("boom"));

        var handler = new ProvisionDealerCommandHandler(context, context, hasher.Object, publisher.Object);

        try { await handler.Handle(ValidCommand(), CancellationToken.None); }
        catch (InvalidOperationException) { /* expected */ }

        publisher.Verify(
            p => p.Publish(It.IsAny<DealerProvisionedDomainEvent>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "no event must be published when the transaction rolls back");
    }

    [Fact]
    public async Task Handle_ShouldLowercaseSubdomain_BeforePersist()
    {
        using var context = CreateContext();
        var publisher = new Mock<MediatR.IPublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<MediatR.INotification>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        var handler = new ProvisionDealerCommandHandler(context, context, HasherReturning().Object, publisher.Object);

        var result = await handler.Handle(ValidCommand("AuToMoToRs"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Subdomain.Should().Be("automotors");
        var settings = await context.DealerSettings
            .IgnoreQueryFilters()
            .FirstAsync(s => s.DealerId == result.Value.DealerId);
        settings.HostName.Should().Be("automotors");
    }
}