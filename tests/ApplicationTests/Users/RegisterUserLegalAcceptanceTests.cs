using Application.Abstractions.Authentication;
using Application.Abstractions.Tenancy;
using Application.Billing.Entitlements;
using Application.Legal;
using Application.UnitTests;
using Application.UnitTests.Legal;
using Application.Users.Register;
using Domain.Legal;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace ApplicationTests.Users;

public class RegisterUserLegalAcceptanceTests
{
    private static readonly Guid DealerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static RegisterUserCommandHandler Handler(TestApplicationDbContext context)
    {
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed");
        var tenant = new Mock<ICurrentTenantService>();
        tenant.Setup(t => t.DealerId).Returns(DealerId);
        tenant.Setup(t => t.HasTenant).Returns(true);

        var entitlements = new Mock<IEntitlementService>();
        entitlements.Setup(s => s.EnsureCanAddAsync(It.IsAny<Guid>(), It.IsAny<PlanResource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        entitlements.Setup(s => s.RoleConsumesASeatAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        return new RegisterUserCommandHandler(context, context, hasher.Object, tenant.Object, FakeLegalTermsProvider.Recorder(context), entitlements.Object);
    }

    private static RegisterUserCommand Command(string? version) =>
        new("a@b.com", "A", "B", "Pass123!", version, "198.51.100.2", "UA/2");

    [Fact]
    public async Task Handle_WithMismatchedTermsVersion_FailsAndCreatesNoUser()
    {
        using var context = CreateContext();

        var result = await Handler(context).Handle(Command("2020-01-01"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(LegalErrors.TermsVersionMismatch);
        (await context.Users.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
        (await context.LegalAcceptances.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithCurrentTermsVersion_RecordsAcceptance()
    {
        using var context = CreateContext();

        var result = await Handler(context).Handle(Command(FakeLegalTermsProvider.CurrentVersion), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var acceptance = await context.LegalAcceptances.SingleAsync();
        acceptance.UserId.Should().Be(result.Value);
        acceptance.DealerId.Should().Be(DealerId);
        acceptance.DocumentKind.Should().Be(LegalDocumentKind.Terms);
        acceptance.Version.Should().Be(FakeLegalTermsProvider.CurrentVersion);
        acceptance.Ip.Should().Be("198.51.100.2");
    }
}
