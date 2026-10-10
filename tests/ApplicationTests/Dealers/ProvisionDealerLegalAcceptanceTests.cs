using Application.Abstractions.Authentication;
using Application.Abstractions.Billing;
using Application.Dealers.Provision;
using Application.Legal;
using Application.UnitTests.Legal;
using Domain.Legal;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ApplicationTests.Dealers;

public class ProvisionDealerLegalAcceptanceTests
{
    private static Application.UnitTests.TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<Application.UnitTests.TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new Application.UnitTests.TestApplicationDbContext(options);
    }

    private static ProvisionDealerCommand Command(string? acceptedVersion) => new(
        DealerName: "Automotors del Sur",
        Subdomain: "automotors",
        AdminEmail: "admin@automotors.com",
        AdminPassword: "Sup3r$ecret!",
        AdminFirstName: "Ana",
        AdminLastName: "García",
        AcceptedTermsVersion: acceptedVersion,
        AcceptanceIp: "203.0.113.7",
        AcceptanceUserAgent: "TestAgent/1.0");

    private static ProvisionDealerCommandHandler Handler(Application.UnitTests.TestApplicationDbContext context)
    {
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");
        var publisher = new Mock<MediatR.IPublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<MediatR.INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var gateway = new Mock<ISubscriptionGateway>();
        return new ProvisionDealerCommandHandler(
            context, context, hasher.Object, publisher.Object, gateway.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProvisionDealerCommandHandler>.Instance,
            FakeLegalTermsProvider.Recorder(context));
    }

    [Theory]
    [InlineData("2020-01-01")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Handle_WithMismatchedOrMissingTermsVersion_FailsAndCreatesNothing(string? version)
    {
        using var context = CreateContext();

        var result = await Handler(context).Handle(Command(version), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(LegalErrors.TermsVersionMismatch);
        result.Error.Code.Should().Be("legal.terms_version_mismatch");
        (await context.DealerSettings.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
        (await context.Users.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
        (await context.LegalAcceptances.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithCurrentTermsVersion_RecordsAppendOnlyAcceptanceForAdmin()
    {
        using var context = CreateContext();

        var result = await Handler(context).Handle(Command(FakeLegalTermsProvider.CurrentVersion), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var acceptance = await context.LegalAcceptances.SingleAsync();
        acceptance.DealerId.Should().Be(result.Value.DealerId);
        acceptance.UserId.Should().Be(result.Value.AdminUserId);
        acceptance.DocumentKind.Should().Be(LegalDocumentKind.Terms);
        acceptance.Version.Should().Be(FakeLegalTermsProvider.CurrentVersion);
        acceptance.ContentSha256.Should().Be(FakeLegalTermsProvider.CurrentSha);
        acceptance.AcceptedAtUtc.Should().Be(new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc));
        acceptance.Ip.Should().Be("203.0.113.7");
        acceptance.UserAgent.Should().Be("TestAgent/1.0");
    }
}
