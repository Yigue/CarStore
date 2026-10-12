using Domain.Users;

namespace DomainTests.Users;

/// <summary>sfc A4.1 / RN-030: single-use, expiring action tokens; only the hash is stored.</summary>
public class UserActionTokenTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static string Hash(char c) => new(c, 64);

    private static UserActionToken Issue(
        UserActionPurpose purpose = UserActionPurpose.EmailVerification,
        string email = "ana@test.com",
        string? hash = null,
        IEnumerable<UserActionToken>? existing = null,
        DateTime? now = null) =>
        UserActionToken.Issue(existing ?? [], purpose, DealerId, email, hash ?? Hash('a'), Guid.NewGuid(), null, null, now ?? Now);

    [Fact]
    public void Issue_StoresHashAndExpiresInTwentyFourHoursForEmailVerification()
    {
        var token = Issue();

        token.TokenHash.Should().Be(Hash('a'));
        token.Purpose.Should().Be(UserActionPurpose.EmailVerification);
        token.DealerId.Should().Be(DealerId);
        token.Email.Should().Be("ana@test.com");
        token.ExpiresAtUtc.Should().Be(Now.AddHours(24));
        token.ConsumedAtUtc.Should().BeNull();
    }

    [Theory]
    [InlineData(UserActionPurpose.EmailVerification, 24)]
    [InlineData(UserActionPurpose.Invitation, 7 * 24)]
    [InlineData(UserActionPurpose.OwnerTransfer, 48)]
    public void LifetimeOf_IsDefinedPerPurpose(UserActionPurpose purpose, int hours)
    {
        UserActionToken.LifetimeOf(purpose).Should().Be(TimeSpan.FromHours(hours));
    }

    [Fact]
    public void Issue_RejectsAnythingThatIsNotASha256Digest()
    {
        Action act = () => Issue(hash: "plain-token-value");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void TryConsume_SucceedsOnceAndFailsTheSecondTime()
    {
        var token = Issue();

        token.TryConsume(Now.AddMinutes(5)).Should().BeTrue();
        token.ConsumedAtUtc.Should().Be(Now.AddMinutes(5));
        token.TryConsume(Now.AddMinutes(6)).Should().BeFalse();
    }

    [Fact]
    public void TryConsume_RejectsAnExpiredToken()
    {
        var token = Issue();

        token.TryConsume(Now.AddHours(24)).Should().BeFalse();
        token.ConsumedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Issue_RevokesPendingTokensOfTheSamePurposeAndEmail()
    {
        var older = Issue(hash: Hash('1'));
        var otherEmail = Issue(email: "bea@test.com", hash: Hash('2'));
        var otherPurpose = Issue(purpose: UserActionPurpose.Invitation, hash: Hash('3'));

        var fresh = Issue(hash: Hash('4'), existing: [older, otherEmail, otherPurpose], now: Now.AddMinutes(1));

        older.TryConsume(Now.AddMinutes(2)).Should().BeFalse("a newer token revoked it");
        otherEmail.IsPending(Now.AddMinutes(2)).Should().BeTrue();
        otherPurpose.IsPending(Now.AddMinutes(2)).Should().BeTrue();
        fresh.IsPending(Now.AddMinutes(2)).Should().BeTrue();
    }

    [Fact]
    public void Issue_MatchesTheEmailCaseInsensitively()
    {
        var older = Issue(email: "Ana@Test.com", hash: Hash('1'));

        Issue(email: "ana@test.com", hash: Hash('2'), existing: [older]);

        older.IsPending(Now).Should().BeFalse();
    }
}
