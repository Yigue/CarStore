using Application.Abstractions.Email;
using Application.Abstractions.Tenancy;
using Application.Marketing.Newsletter;
using Application.Marketing.Newsletter.Tokens;
using Domain.Marketing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Application.UnitTests.Marketing;

/// <summary>
/// sfc B5.1 / RN-019: subscribing creates a pending <see cref="Subscriber"/> (never a Client) and emails a double
/// opt-in link. The response is identical whether or not the address already exists, so the endpoint cannot be used
/// to find out who is subscribed. The emailed links confirm and unsubscribe in one step.
/// </summary>
public class SubscribeCommandHandlerTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    // The email is queued, never sent on the request path: the answer time must not depend on whether one goes out.
    private readonly Mock<IBackgroundEmailQueue> _email = new();
    private readonly List<IReadOnlyDictionary<string, string>> _sent = [];

    public SubscribeCommandHandlerTests()
    {
        _email.Setup(e => e.TryEnqueue(EmailTemplate.NewsletterConfirm, It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Callback<EmailTemplate, string, IReadOnlyDictionary<string, string>>((_, _, model) => _sent.Add(model))
            .Returns(true);
    }

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, DealerId);

    private SubscribeCommandHandler Handler(TestApplicationDbContext context)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = "https://app.test" }).Build();
        return new SubscribeCommandHandler(context, tenant.Object, new FakeDateTimeProvider { UtcNow = Now }, _email.Object,
            configuration, NullLogger<SubscribeCommandHandler>.Instance);
    }

    private static string TokenFrom(string url) => url[(url.IndexOf("t=", StringComparison.Ordinal) + 2)..];

    [Fact]
    public async Task A_new_address_becomes_a_pending_subscriber_and_gets_the_opt_in_email_but_no_client()
    {
        using var context = CreateContext();

        var result = await Handler(context).Handle(new SubscribeCommand("Persona@Example.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Subscriber subscriber = await context.Set<Subscriber>().SingleAsync();
        subscriber.Status.Should().Be(SubscriberStatus.Pending);
        subscriber.EmailNormalized.Should().Be("persona@example.com");
        (await context.Clients.CountAsync()).Should().Be(0);
        _sent.Should().ContainSingle();
        _sent[0]["ConfirmUrl"].Should().StartWith("https://app.test/newsletter/confirmar?t=");
        _sent[0]["UnsubscribeUrl"].Should().StartWith("https://app.test/newsletter/baja?t=");
    }

    [Fact]
    public async Task An_already_confirmed_address_gets_the_same_answer_and_no_email()
    {
        using var context = CreateContext();
        await Handler(context).Handle(new SubscribeCommand("a@b.com"), CancellationToken.None);
        (await context.Set<Subscriber>().SingleAsync()).Confirm(Now);
        await context.SaveChangesAsync();
        _sent.Clear();

        var again = await Handler(context).Handle(new SubscribeCommand("A@B.com"), CancellationToken.None);

        again.IsSuccess.Should().BeTrue();
        (await context.Set<Subscriber>().CountAsync()).Should().Be(1);
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task A_pending_address_gets_a_fresh_opt_in_email_without_a_duplicate_row()
    {
        using var context = CreateContext();
        await Handler(context).Handle(new SubscribeCommand("a@b.com"), CancellationToken.None);

        await Handler(context).Handle(new SubscribeCommand("a@b.com"), CancellationToken.None);

        (await context.Set<Subscriber>().CountAsync()).Should().Be(1);
        _sent.Should().HaveCount(2);
        TokenFrom(_sent[1]["ConfirmUrl"]).Should().NotBe(TokenFrom(_sent[0]["ConfirmUrl"]));
    }

    [Fact]
    public async Task A_full_email_queue_still_answers_success()
    {
        using var context = CreateContext();
        _email.Setup(e => e.TryEnqueue(It.IsAny<EmailTemplate>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Returns(false);

        var result = await Handler(context).Handle(new SubscribeCommand("a@b.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task An_invalid_address_is_rejected()
    {
        using var context = CreateContext();

        var result = await Handler(context).Handle(new SubscribeCommand("nope"), CancellationToken.None);

        result.Error.Code.Should().Be("Newsletter.InvalidEmail");
    }

    [Fact]
    public async Task The_emailed_links_confirm_and_then_unsubscribe()
    {
        using var context = CreateContext();
        await Handler(context).Handle(new SubscribeCommand("a@b.com"), CancellationToken.None);
        var clock = new FakeDateTimeProvider { UtcNow = Now.AddHours(1) };

        var confirmed = await new ConfirmSubscriptionCommandHandler(context, clock)
            .Handle(new ConfirmSubscriptionCommand(TokenFrom(_sent[0]["ConfirmUrl"])), CancellationToken.None);
        confirmed.IsSuccess.Should().BeTrue();
        (await context.Set<Subscriber>().SingleAsync()).Status.Should().Be(SubscriberStatus.Confirmed);

        var unsubscribed = await new UnsubscribeCommandHandler(context, clock)
            .Handle(new UnsubscribeCommand(TokenFrom(_sent[0]["UnsubscribeUrl"])), CancellationToken.None);
        unsubscribed.IsSuccess.Should().BeTrue();
        (await context.Set<Subscriber>().SingleAsync()).Status.Should().Be(SubscriberStatus.Unsubscribed);
    }

    [Fact]
    public async Task An_unknown_token_is_rejected()
    {
        using var context = CreateContext();
        var clock = new FakeDateTimeProvider { UtcNow = Now };

        (await new ConfirmSubscriptionCommandHandler(context, clock).Handle(new ConfirmSubscriptionCommand("bogus"), CancellationToken.None))
            .Error.Code.Should().Be("Newsletter.InvalidToken");
        (await new UnsubscribeCommandHandler(context, clock).Handle(new UnsubscribeCommand("bogus"), CancellationToken.None))
            .Error.Code.Should().Be("Newsletter.InvalidToken");
    }
}
