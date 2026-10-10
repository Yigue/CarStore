using Domain.Legal;
using FluentAssertions;
using SharedKernel;

namespace DomainTests.Legal;

public class LegalAcceptanceTests
{
    private static readonly string ValidHash = new('a', 64);
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WithValidVersionAndHash_PopulatesAllFields()
    {
        var dealerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var acceptance = LegalAcceptance.Create(
            dealerId, userId, LegalDocumentKind.Terms, "2026-10-01", ValidHash, Now, "10.0.0.1", "UA/1.0");

        acceptance.Id.Should().NotBe(Guid.Empty);
        acceptance.DealerId.Should().Be(dealerId);
        acceptance.UserId.Should().Be(userId);
        acceptance.DocumentKind.Should().Be(LegalDocumentKind.Terms);
        acceptance.Version.Should().Be("2026-10-01");
        acceptance.ContentSha256.Should().Be(ValidHash);
        acceptance.AcceptedAtUtc.Should().Be(Now);
        acceptance.Ip.Should().Be("10.0.0.1");
        acceptance.UserAgent.Should().Be("UA/1.0");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2026-1-1")]
    [InlineData("v1")]
    public void Create_WithInvalidVersion_Throws(string version)
    {
        var act = () => LegalAcceptance.Create(
            Guid.NewGuid(), Guid.NewGuid(), LegalDocumentKind.Terms, version, ValidHash, Now, null, null);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void Create_WithInvalidHash_Throws(string hash)
    {
        var act = () => LegalAcceptance.Create(
            Guid.NewGuid(), Guid.NewGuid(), LegalDocumentKind.Terms, "2026-10-01", hash, Now, null, null);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WithEmptyDealerOrUser_Throws()
    {
        var noDealer = () => LegalAcceptance.Create(
            Guid.Empty, Guid.NewGuid(), LegalDocumentKind.Terms, "2026-10-01", ValidHash, Now, null, null);
        var noUser = () => LegalAcceptance.Create(
            Guid.NewGuid(), Guid.Empty, LegalDocumentKind.Terms, "2026-10-01", ValidHash, Now, null, null);

        noDealer.Should().Throw<DomainException>();
        noUser.Should().Throw<DomainException>();
    }
}
