using Application.Abstractions.Data;
using Application.Abstractions.Email;
using Application.UnitTests;
using Application.Users.Verification;
using Application.Users.Verification.ResendVerification;
using Application.Users.Verification.VerifyEmail;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SharedKernel;

namespace ApplicationTests.Users;

/// <summary>sfc A4.1 / RN-030: real, single-use, expiring email verification; only the SHA-256 is persisted.</summary>
public class VerifyEmailCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static async Task<User> SeedUserAsync(TestApplicationDbContext context, string email = "ana@test.com")
    {
        var user = new User(Guid.NewGuid(), email, "Ana", "<b>Gomez</b>", "hash", Guid.NewGuid());
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private sealed class CapturedEmail
    {
        public List<(EmailTemplate Template, string Recipient, IReadOnlyDictionary<string, string> Model)> Sent { get; } = [];
        public Mock<ITransactionalEmailSender> Mock { get; } = new();

        public CapturedEmail()
        {
            Mock.Setup(s => s.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .Callback<EmailTemplate, string, IReadOnlyDictionary<string, string>, CancellationToken>((t, r, m, _) => Sent.Add((t, r, m)))
                .Returns(Task.CompletedTask);
        }
    }

    private static ResendVerificationCommandHandler CreateResend(IApplicationDbContext context, CapturedEmail email)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = "https://app.example.com/" })
            .Build();
        return new ResendVerificationCommandHandler(context, email.Mock.Object, configuration, NullLogger<ResendVerificationCommandHandler>.Instance);
    }

    private static string TokenFrom(CapturedEmail email)
    {
        string url = email.Sent.Single().Model["VerifyUrl"];
        url.Should().StartWith("https://app.example.com/verificar?token=");
        return url.Split("token=")[1];
    }

    [Fact]
    public async Task Resend_PersistsOnlyTheSha256OfTheEmailedToken()
    {
        using var context = CreateContext();
        await SeedUserAsync(context);
        var email = new CapturedEmail();

        Result result = await CreateResend(context, email).Handle(new ResendVerificationCommand("ana@test.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        email.Sent.Single().Template.Should().Be(EmailTemplate.VerifyEmail);
        email.Sent.Single().Recipient.Should().Be("ana@test.com");
        email.Sent.Single().Model["Name"].Should().Be("Ana");
        email.Sent.Single().Model["ExpiresInHours"].Should().Be("24");
        string plain = TokenFrom(email);

        UserActionToken stored = await context.UserActionTokens.SingleAsync();
        stored.TokenHash.Should().Be(UserActionTokenHasher.Hash(plain));
        stored.TokenHash.Should().NotBe(plain);
        stored.TokenHash.Should().MatchRegex("^[0-9A-F]{64}$");
        stored.Purpose.Should().Be(UserActionPurpose.EmailVerification);
        plain.Length.Should().BeGreaterOrEqualTo(43, "32 random bytes in base64url");
        (await context.UserActionTokens.Select(t => t.TokenHash).ToListAsync()).Should().NotContain(plain);
    }

    [Fact]
    public async Task Resend_AnswersTheSameWhetherTheAccountExistsOrNot()
    {
        using var context = CreateContext();
        await SeedUserAsync(context);
        var knownEmail = new CapturedEmail();
        var unknownEmail = new CapturedEmail();

        Result known = await CreateResend(context, knownEmail).Handle(new ResendVerificationCommand("ana@test.com"), CancellationToken.None);
        Result unknown = await CreateResend(context, unknownEmail).Handle(new ResendVerificationCommand("nadie@test.com"), CancellationToken.None);

        known.IsSuccess.Should().BeTrue();
        unknown.IsSuccess.Should().BeTrue();
        unknown.Should().BeEquivalentTo(known);
        unknownEmail.Sent.Should().BeEmpty();
        (await context.UserActionTokens.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Resend_DoesNothingForAnAlreadyVerifiedAccount()
    {
        using var context = CreateContext();
        User user = await SeedUserAsync(context);
        user.MarkEmailVerified(DateTime.UtcNow);
        await context.SaveChangesAsync();
        var email = new CapturedEmail();

        Result result = await CreateResend(context, email).Handle(new ResendVerificationCommand("ana@test.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        email.Sent.Should().BeEmpty();
        (await context.UserActionTokens.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Resend_RevokesThePreviousPendingToken()
    {
        using var context = CreateContext();
        await SeedUserAsync(context);
        var first = new CapturedEmail();
        var second = new CapturedEmail();

        await CreateResend(context, first).Handle(new ResendVerificationCommand("ana@test.com"), CancellationToken.None);
        await CreateResend(context, second).Handle(new ResendVerificationCommand("ana@test.com"), CancellationToken.None);

        Result oldToken = await new VerifyEmailCommandHandler(context).Handle(new VerifyEmailCommand(TokenFrom(first)), CancellationToken.None);
        Result newToken = await new VerifyEmailCommandHandler(context).Handle(new VerifyEmailCommand(TokenFrom(second)), CancellationToken.None);

        oldToken.IsFailure.Should().BeTrue();
        newToken.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Resend_DoesNotFailWhenTheEmailTransportFails()
    {
        using var context = CreateContext();
        await SeedUserAsync(context);
        var email = new CapturedEmail();
        email.Mock.Setup(s => s.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));

        Result result = await CreateResend(context, email).Handle(new ResendVerificationCommand("ana@test.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("a transport error must not reveal that the account exists");
    }

    /// <summary>
    /// Counts how often a handler touches each table set and calls SaveChanges. Each set access is one query
    /// round-trip in the handler, so this compares the database work done per scenario.
    /// </summary>
    private sealed class WorkCounter
    {
        public int Queries { get; set; }
        public int Saves { get; set; }
    }

    private static async Task<(Result Result, int Queries, int Saves, CapturedEmail Email)> ResendCountingAsync(
        string databaseName, string address, bool transportFails = false)
    {
        var counter = new WorkCounter();
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        using var real = new TestApplicationDbContext(options);
        var context = new Mock<IApplicationDbContext>();
        context.Setup(c => c.Users).Returns(() => { counter.Queries++; return real.Users; });
        context.Setup(c => c.UserActionTokens).Returns(() => { counter.Queries++; return real.UserActionTokens; });
        context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct => { counter.Saves++; return real.SaveChangesAsync(ct); });
        var email = new CapturedEmail();
        if (transportFails)
        {
            email.Mock.Setup(s => s.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("smtp down"));
        }

        Result result = await CreateResend(context.Object, email).Handle(new ResendVerificationCommand(address), CancellationToken.None);
        return (result, counter.Queries, counter.Saves, email);
    }

    [Fact]
    public async Task Resend_DoesTheSameDatabaseWorkForUnknownVerifiableAndVerifiedAccounts()
    {
        string db = Guid.NewGuid().ToString();
        var seedOptions = new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(db).Options;
        using (var seed = new TestApplicationDbContext(seedOptions))
        {
            await SeedUserAsync(seed, "ana@test.com");
            User verified = await SeedUserAsync(seed, "verified@test.com");
            verified.MarkEmailVerified(DateTime.UtcNow);
            await seed.SaveChangesAsync();
        }

        var unknown = await ResendCountingAsync(db, "nadie@test.com");
        var verifiable = await ResendCountingAsync(db, "ana@test.com");
        var alreadyVerified = await ResendCountingAsync(db, "verified@test.com");

        verifiable.Queries.Should().BeGreaterThan(0);
        unknown.Queries.Should().Be(verifiable.Queries, "an unknown email must cost the same reads as a verifiable one");
        alreadyVerified.Queries.Should().Be(verifiable.Queries);
        unknown.Saves.Should().Be(verifiable.Saves);
        alreadyVerified.Saves.Should().Be(verifiable.Saves);
        unknown.Result.Should().BeEquivalentTo(verifiable.Result);
        alreadyVerified.Result.Should().BeEquivalentTo(verifiable.Result);
        unknown.Email.Sent.Should().BeEmpty();
        alreadyVerified.Email.Sent.Should().BeEmpty();
        verifiable.Email.Sent.Should().HaveCount(1);
    }

    [Fact]
    public async Task Resend_TransportFailureLeavesTheResultIdenticalToTheUnknownAccountResult()
    {
        string db = Guid.NewGuid().ToString();
        var seedOptions = new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(db).Options;
        using (var seed = new TestApplicationDbContext(seedOptions))
        {
            await SeedUserAsync(seed);
        }

        var failing = await ResendCountingAsync(db, "ana@test.com", transportFails: true);
        var unknown = await ResendCountingAsync(db, "nadie@test.com");

        failing.Result.Should().BeEquivalentTo(unknown.Result);
        failing.Queries.Should().Be(unknown.Queries);
    }

    [Fact]
    public async Task Verify_WithTheEmailedToken_MarksTheEmailVerifiedAndConsumesTheToken()
    {
        using var context = CreateContext();
        User user = await SeedUserAsync(context);
        var email = new CapturedEmail();
        await CreateResend(context, email).Handle(new ResendVerificationCommand("ana@test.com"), CancellationToken.None);
        string plain = TokenFrom(email);

        Result result = await new VerifyEmailCommandHandler(context).Handle(new VerifyEmailCommand(plain), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.Users.SingleAsync(u => u.Id == user.Id)).EmailVerifiedAtUtc.Should().NotBeNull();
        (await context.UserActionTokens.SingleAsync()).ConsumedAtUtc.Should().NotBeNull();

        Result second = await new VerifyEmailCommandHandler(context).Handle(new VerifyEmailCommand(plain), CancellationToken.None);
        second.IsFailure.Should().BeTrue();
        second.Error.Should().Be(UserErrors.InvalidVerificationToken);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-real-token")]
    public async Task Verify_RejectsAnUnknownToken_WithoutChangingAnyUser(string token)
    {
        using var context = CreateContext();
        User user = await SeedUserAsync(context);

        Result result = await new VerifyEmailCommandHandler(context).Handle(new VerifyEmailCommand(token), CancellationToken.None);

        result.Error.Should().Be(UserErrors.InvalidVerificationToken);
        (await context.Users.SingleAsync(u => u.Id == user.Id)).EmailVerifiedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Verify_RejectsAnExpiredToken()
    {
        using var context = CreateContext();
        User user = await SeedUserAsync(context);
        const string plain = "expired-token-value";
        context.UserActionTokens.Add(UserActionToken.Issue(
            [], UserActionPurpose.EmailVerification, user.DealerId, "ana@test.com",
            UserActionTokenHasher.Hash(plain), user.Id, null, null, DateTime.UtcNow.AddHours(-25)));
        await context.SaveChangesAsync();

        Result result = await new VerifyEmailCommandHandler(context).Handle(new VerifyEmailCommand(plain), CancellationToken.None);

        result.Error.Should().Be(UserErrors.InvalidVerificationToken);
        (await context.Users.SingleAsync(u => u.Id == user.Id)).EmailVerifiedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Verify_RejectsATokenOfAnotherPurpose()
    {
        using var context = CreateContext();
        User user = await SeedUserAsync(context);
        const string plain = "invitation-token-value";
        context.UserActionTokens.Add(UserActionToken.Issue(
            [], UserActionPurpose.Invitation, user.DealerId, "ana@test.com",
            UserActionTokenHasher.Hash(plain), user.Id, null, null, DateTime.UtcNow));
        await context.SaveChangesAsync();

        Result result = await new VerifyEmailCommandHandler(context).Handle(new VerifyEmailCommand(plain), CancellationToken.None);

        result.Error.Should().Be(UserErrors.InvalidVerificationToken);
    }
}
