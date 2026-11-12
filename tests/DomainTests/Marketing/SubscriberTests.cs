using Domain.Marketing;
using FluentAssertions;
using SharedKernel;

namespace DomainTests.Marketing;

/// <summary>
/// sfc B5.1 / RN-019: a newsletter subscriber is a marketing contact, not a <c>Client</c>. Double opt-in: it starts
/// Pending and only a confirmation link makes it Confirmed. Unsubscribing is one click (RFC 8058) and final until the
/// person subscribes again, which restarts the opt-in.
/// </summary>
public class SubscriberTests
{
    private static readonly Guid DealerId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
    private const string ConfirmHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string UnsubscribeHash = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

    private static Subscriber NewPending(string email = "Persona@Example.com ") =>
        Subscriber.Subscribe(DealerId, email, "web", ConfirmHash, UnsubscribeHash, Now).Value;

    [Fact]
    public void A_new_subscriber_is_pending_with_a_normalized_email()
    {
        Subscriber subscriber = NewPending();

        subscriber.Status.Should().Be(SubscriberStatus.Pending);
        subscriber.EmailNormalized.Should().Be("persona@example.com");
        subscriber.DealerId.Should().Be(DealerId);
        subscriber.ConfirmedAtUtc.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void An_invalid_email_is_rejected(string email)
    {
        Result<Subscriber> result = Subscriber.Subscribe(DealerId, email, "web", ConfirmHash, UnsubscribeHash, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Newsletter.InvalidEmail");
    }

    [Fact]
    public void A_token_that_is_not_a_sha256_digest_is_rejected()
    {
        Action act = () => Subscriber.Subscribe(DealerId, "a@b.com", "web", "plain-secret", UnsubscribeHash, Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Confirming_within_the_window_makes_it_confirmed_and_burns_the_confirm_token()
    {
        Subscriber subscriber = NewPending();

        subscriber.Confirm(Now.AddHours(1)).Should().BeTrue();

        subscriber.Status.Should().Be(SubscriberStatus.Confirmed);
        subscriber.ConfirmedAtUtc.Should().Be(Now.AddHours(1));
        subscriber.ConfirmTokenHash.Should().BeNull();
    }

    [Fact]
    public void An_expired_confirmation_does_not_confirm()
    {
        Subscriber subscriber = NewPending();

        subscriber.Confirm(Now + Subscriber.ConfirmationLifetime + TimeSpan.FromMinutes(1)).Should().BeFalse();

        subscriber.Status.Should().Be(SubscriberStatus.Pending);
    }

    [Fact]
    public void Unsubscribing_is_one_click_and_idempotent()
    {
        Subscriber subscriber = NewPending();
        subscriber.Confirm(Now);

        subscriber.Unsubscribe(Now.AddDays(1));
        subscriber.Unsubscribe(Now.AddDays(2));

        subscriber.Status.Should().Be(SubscriberStatus.Unsubscribed);
        subscriber.UnsubscribedAtUtc.Should().Be(Now.AddDays(1));
    }

    [Fact]
    public void Subscribing_again_after_unsubscribing_restarts_the_double_opt_in()
    {
        Subscriber subscriber = NewPending();
        subscriber.Confirm(Now);
        subscriber.Unsubscribe(Now.AddDays(1));

        bool reissued = subscriber.Resubscribe(ConfirmHash, Now.AddDays(2));

        reissued.Should().BeTrue();
        subscriber.Status.Should().Be(SubscriberStatus.Pending);
        subscriber.ConfirmTokenHash.Should().Be(ConfirmHash);
        subscriber.UnsubscribedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Subscribing_again_while_confirmed_changes_nothing()
    {
        Subscriber subscriber = NewPending();
        subscriber.Confirm(Now);

        subscriber.Resubscribe(ConfirmHash, Now.AddDays(2)).Should().BeFalse();

        subscriber.Status.Should().Be(SubscriberStatus.Confirmed);
    }
}
