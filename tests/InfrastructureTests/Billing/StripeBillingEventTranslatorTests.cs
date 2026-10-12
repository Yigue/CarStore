using Application.Abstractions.Billing;
using Infrastructure.Billing;

namespace InfrastructureTests.Billing;

/// <summary>sfc A2.1.3 (ADR C14): the Stripe translator turns a raw webhook payload into a normalized BillingEvent.</summary>
public class StripeBillingEventTranslatorTests
{
    private const long Created = 1700000000;
    private static readonly DateTime CreatedUtc = DateTimeOffset.FromUnixTimeSeconds(Created).UtcDateTime;

    private readonly StripeBillingEventTranslator _translator = new();

    private static string Event(string type, string dataObject, long? created = Created) =>
        $$"""{ "id": "evt_1", "type": "{{type}}", {{(created.HasValue ? $"\"created\": {created}," : "")}} "data": { "object": {{dataObject}} } }""";

    [Fact]
    public void SubscriptionUpdated_MapsToSubscriptionChanged()
    {
        var dealerId = Guid.NewGuid();
        var json = Event("customer.subscription.updated", $$"""
            { "id": "sub_1", "customer": "cus_1", "status": "past_due",
              "current_period_start": 1700000000, "current_period_end": 1701209600, "trial_end": 1701209600,
              "items": { "data": [ { "price": { "id": "price_1" } } ] },
              "metadata": { "dealer_id": "{{dealerId}}" } }
            """);

        var evt = _translator.Translate(json).Should().BeOfType<SubscriptionChanged>().Subject;

        evt.ProviderEventId.Should().Be("evt_1");
        evt.OccurredAtUtc.Should().Be(CreatedUtc);
        evt.CustomerId.Should().Be("cus_1");
        evt.SubscriptionId.Should().Be("sub_1");
        evt.ProviderStatus.Should().Be("past_due");
        evt.PeriodStart.Should().Be(CreatedUtc);
        evt.PeriodEnd.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1701209600).UtcDateTime);
        evt.TrialEnd.Should().Be(evt.PeriodEnd);
        evt.PriceId.Should().Be("price_1");
        evt.DealerId.Should().Be(dealerId);
    }

    [Fact]
    public void SubscriptionDeleted_MapsToSubscriptionChangedCanceled()
    {
        var json = Event("customer.subscription.deleted", """{ "id": "sub_1", "customer": "cus_1" }""");

        var evt = _translator.Translate(json).Should().BeOfType<SubscriptionChanged>().Subject;

        evt.ProviderStatus.Should().Be("canceled");
        evt.CustomerId.Should().Be("cus_1");
        evt.PeriodStart.Should().BeNull();
    }

    [Fact]
    public void InvoicePaymentSucceeded_MapsToPaymentSucceeded()
    {
        var json = Event("invoice.payment_succeeded",
            """{ "customer": "cus_1", "subscription": "sub_1", "period_start": 1700000000, "period_end": 1701209600 }""");

        var evt = _translator.Translate(json).Should().BeOfType<PaymentSucceeded>().Subject;

        evt.CustomerId.Should().Be("cus_1");
        evt.SubscriptionId.Should().Be("sub_1");
        evt.PeriodStart.Should().Be(CreatedUtc);
        evt.PeriodEnd.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1701209600).UtcDateTime);
    }

    [Fact]
    public void InvoicePaymentFailed_CarriesAttemptCount()
    {
        var json = Event("invoice.payment_failed",
            """{ "customer": "cus_1", "subscription": "sub_1", "attempt_count": 3 }""");

        var evt = _translator.Translate(json).Should().BeOfType<PaymentFailed>().Subject;

        evt.Attempt.Should().Be(3);
        evt.CustomerId.Should().Be("cus_1");
    }

    [Fact]
    public void InvoicePaymentFailed_WithoutAttemptCount_DefaultsToOne()
    {
        var json = Event("invoice.payment_failed", """{ "customer": "cus_1", "subscription": "sub_1" }""");

        _translator.Translate(json).Should().BeOfType<PaymentFailed>().Which.Attempt.Should().Be(1);
    }

    [Fact]
    public void TrialWillEnd_MapsToTrialWillEnd()
    {
        var json = Event("customer.subscription.trial_will_end",
            """{ "id": "sub_1", "customer": "cus_1", "trial_end": 1701209600 }""");

        var evt = _translator.Translate(json).Should().BeOfType<TrialWillEnd>().Subject;

        evt.SubscriptionId.Should().Be("sub_1");
        evt.TrialEndsAtUtc.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1701209600).UtcDateTime);
    }

    [Fact]
    public void ChargeRefunded_MapsAmountsInvoiceAndReason()
    {
        var json = Event("charge.refunded", """
            { "id": "ch_1", "customer": "cus_1", "amount": 5000, "amount_refunded": 2000, "currency": "usd",
              "invoice": "in_1", "refunds": { "data": [ { "reason": "requested_by_customer" } ] } }
            """);

        var evt = _translator.Translate(json).Should().BeOfType<ChargeRefunded>().Subject;

        evt.ChargeId.Should().Be("ch_1");
        evt.InvoiceId.Should().Be("in_1");
        evt.Amount.Should().Be(5000);
        evt.AmountRefunded.Should().Be(2000);
        evt.Currency.Should().Be("usd");
        evt.Reason.Should().Be("requested_by_customer");
        evt.IsFullRefund.Should().BeFalse();
    }

    [Fact]
    public void ChargeRefunded_WhenAmountRefundedEqualsAmount_IsFullRefund()
    {
        var json = Event("charge.refunded",
            """{ "id": "ch_1", "customer": "cus_1", "amount": 5000, "amount_refunded": 5000, "currency": "usd" }""");

        _translator.Translate(json).Should().BeOfType<ChargeRefunded>().Which.IsFullRefund.Should().BeTrue();
    }

    [Fact]
    public void CheckoutSessionCompleted_MapsToCheckoutCompleted()
    {
        var dealerId = Guid.NewGuid();
        var json = Event("checkout.session.completed", $$"""
            { "customer": "cus_1", "subscription": "sub_1", "metadata": { "dealer_id": "{{dealerId}}" } }
            """);

        var evt = _translator.Translate(json).Should().BeOfType<CheckoutCompleted>().Subject;

        evt.CustomerId.Should().Be("cus_1");
        evt.SubscriptionId.Should().Be("sub_1");
        evt.DealerId.Should().Be(dealerId);
    }

    [Fact]
    public void UnsupportedEventType_ReturnsNull()
    {
        _translator.Translate(Event("customer.created", """{ "id": "cus_1" }""")).Should().BeNull();
    }

    [Fact]
    public void MissingCreated_FallsBackToNow()
    {
        var json = Event("invoice.payment_failed", """{ "customer": "cus_1" }""", created: null);

        var evt = _translator.Translate(json)!;

        evt.OccurredAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void PayloadWithoutDataObject_Throws()
    {
        var act = () => _translator.Translate("""{ "id": "evt_1", "type": "invoice.payment_failed" }""");

        act.Should().Throw<ArgumentException>();
    }
}
