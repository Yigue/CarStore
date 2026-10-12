using Application.Abstractions.Billing;
using Application.Billing.EventHandlers;
using Application.Platform.AuditLogs;
using Domain.Billing;
using Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureTests.Billing;

/// <summary>
/// sfc A2.2 / A13 / RN-024: <c>charge.refunded</c> becomes a <see cref="BillingRefundRecord"/> (full vs partial),
/// a platform audit entry and an email to the owner. It never touches the subscription state.
/// </summary>
public class ChargeRefundedHandlerTests : IDisposable
{
    private static readonly DateTime T1 = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly BillingHandlerTestDb _t = new();
    private readonly ChargeRefundedHandler _handler;

    public ChargeRefundedHandlerTests()
    {
        _handler = new ChargeRefundedHandler(
            _t.Db, new PlatformAuditLogRecorder(_t.Db), _t.Email, BillingHandlerTestLogger.For<ChargeRefundedHandler>());
    }

    public void Dispose() => _t.Dispose();

    private static ChargeRefunded Refund(long amount, long refunded, string id = "evt_r1", string? reason = "requested_by_customer") =>
        new(id, T1, BillingHandlerTestDb.CustomerId, "ch_1", "in_1", amount, refunded, "usd", reason);

    [Fact]
    public async Task FullRefund_CreatesFullRecordWithAmountCurrencyInvoiceAndReason()
    {
        await _t.SeedAsync();

        await _handler.Handle(Refund(5000, 5000), CancellationToken.None);
        await _t.Db.SaveChangesAsync();

        var record = await _t.Db.BillingRefundRecords.AsNoTracking().SingleAsync();
        record.DealerId.Should().Be(_t.DealerId);
        record.SubscriptionId.Should().Be(_t.Subscription.Id);
        record.Kind.Should().Be(BillingRefundKind.Full);
        record.AmountRefundedMinor.Should().Be(5000);
        record.ChargeAmountMinor.Should().Be(5000);
        record.Currency.Should().Be("usd");
        record.InvoiceId.Should().Be("in_1");
        record.ChargeId.Should().Be("ch_1");
        record.Reason.Should().Be("requested_by_customer");
        record.ProviderEventId.Should().Be("evt_r1");
        record.OccurredAtUtc.Should().Be(T1);
    }

    [Fact]
    public async Task PartialRefund_CreatesPartialRecordWithExactRefundedAmount()
    {
        await _t.SeedAsync();

        await _handler.Handle(Refund(5000, 1250, "evt_r2"), CancellationToken.None);
        await _t.Db.SaveChangesAsync();

        var record = await _t.Db.BillingRefundRecords.AsNoTracking().SingleAsync();
        record.Kind.Should().Be(BillingRefundKind.Partial);
        record.AmountRefundedMinor.Should().Be(1250);
        record.ChargeAmountMinor.Should().Be(5000);
    }

    [Fact]
    public async Task FullAndPartial_AreDistinctRecords()
    {
        await _t.SeedAsync();

        await _handler.Handle(Refund(5000, 1250, "evt_p"), CancellationToken.None);
        await _handler.Handle(Refund(5000, 5000, "evt_f"), CancellationToken.None);
        await _t.Db.SaveChangesAsync();

        var kinds = await _t.Db.BillingRefundRecords.AsNoTracking().OrderBy(r => r.AmountRefundedMinor).Select(r => r.Kind).ToListAsync();
        kinds.Should().Equal(BillingRefundKind.Partial, BillingRefundKind.Full);
    }

    [Fact]
    public async Task Refund_DoesNotChangeSubscriptionState()
    {
        await _t.SeedAsync();

        await _handler.Handle(Refund(5000, 5000), CancellationToken.None);
        await _t.Db.SaveChangesAsync();

        var sub = await _t.Db.DealerSubscriptions.AsNoTracking().SingleAsync();
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.LastProviderEventAtUtc.Should().BeNull();
        sub.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Refund_RecordsPlatformAuditEntry()
    {
        await _t.SeedAsync();

        await _handler.Handle(Refund(5000, 1250), CancellationToken.None);
        await _t.Db.SaveChangesAsync();

        var entry = await _t.Db.PlatformAuditLogs.AsNoTracking().SingleAsync();
        entry.Action.Should().Be(PlatformAuditAction.SubscriptionRefunded);
        entry.ActorKind.Should().Be(PlatformAuditActorKind.System);
        entry.DealerId.Should().Be(_t.DealerId);
        entry.DealerName.Should().Be("Autos del Sur");
        entry.SourceEventKey.Should().Be("billing-refund:evt_r1");
        entry.OccurredAtUtc.Should().Be(T1);
    }

    [Fact]
    public async Task Refund_NotifiesOwnerAndBillingEmail_InSpanish()
    {
        await _t.SeedAsync(billingEmail: "facturacion@autosdelsur.test");

        await _handler.Handle(Refund(5000, 1250), CancellationToken.None);

        _t.Email.Sent.Select(e => e.To).Should().BeEquivalentTo("owner@autosdelsur.test", "facturacion@autosdelsur.test");
        var mail = _t.Email.Sent[0];
        mail.Subject.Should().Contain("reembolso").And.Contain("parcial");
        mail.Body.Should().Contain("12,50").And.Contain("USD").And.Contain("Marta");
    }

    [Fact]
    public async Task FullRefundEmail_SaysTotal()
    {
        await _t.SeedAsync();

        await _handler.Handle(Refund(5000, 5000), CancellationToken.None);

        _t.Email.Sent.Should().ContainSingle().Which.Subject.Should().Contain("total");
    }

    [Fact]
    public async Task DuplicateEvent_CreatesOneRecordAndSendsOneEmail()
    {
        await _t.SeedAsync();

        await _handler.Handle(Refund(5000, 5000), CancellationToken.None);
        await _t.Db.SaveChangesAsync();
        await _handler.Handle(Refund(5000, 5000), CancellationToken.None);
        await _t.Db.SaveChangesAsync();

        (await _t.Db.BillingRefundRecords.CountAsync()).Should().Be(1);
        (await _t.Db.PlatformAuditLogs.CountAsync()).Should().Be(1);
        _t.Email.Sent.Should().HaveCount(1);
    }

    [Fact]
    public async Task UnknownCustomer_IsIgnored()
    {
        await _t.SeedAsync();

        await _handler.Handle(Refund(5000, 5000) with { CustomerId = "cus_unknown" }, CancellationToken.None);
        await _t.Db.SaveChangesAsync();

        (await _t.Db.BillingRefundRecords.CountAsync()).Should().Be(0);
        _t.Email.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task EmailFailure_DoesNotFailTheHandlerNorLoseTheRecord()
    {
        await _t.SeedAsync();
        _t.Email.ThrowOnSend = true;

        var act = () => _handler.Handle(Refund(5000, 5000), CancellationToken.None);

        await act.Should().NotThrowAsync();
        await _t.Db.SaveChangesAsync();
        (await _t.Db.BillingRefundRecords.CountAsync()).Should().Be(1);
    }
}
