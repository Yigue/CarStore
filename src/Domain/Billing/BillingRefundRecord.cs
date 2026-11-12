using SharedKernel;

namespace Domain.Billing;

public enum BillingRefundKind
{
    Full,
    Partial,
}

/// <summary>
/// sfc A2.2 / A13 / RN-024: one provider <c>charge.refunded</c> event recorded against the dealer subscription.
/// Informational only: it never changes the subscription state. Amounts are in the currency's minor unit.
/// Retained by the tenant purge (billing history), so it has no foreign key to tenant data.
/// </summary>
public sealed class BillingRefundRecord : Entity
{
    public const string ProviderEventUniqueIndex = "ux_billing_refund_records_provider_event_id";

    private BillingRefundRecord()
    {
    }

    public Guid SubscriptionId { get; private set; }
    public string ProviderEventId { get; private set; } = string.Empty;
    public string ChargeId { get; private set; } = string.Empty;
    public string? InvoiceId { get; private set; }
    public long ChargeAmountMinor { get; private set; }
    public long AmountRefundedMinor { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public BillingRefundKind Kind { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }

    public static BillingRefundRecord Record(
        Guid dealerId,
        Guid subscriptionId,
        string providerEventId,
        string chargeId,
        string? invoiceId,
        long chargeAmountMinor,
        long amountRefundedMinor,
        string currency,
        string? reason,
        DateTime occurredAtUtc,
        DateTime recordedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(providerEventId))
            throw new DomainException("BillingRefundRecord requires a ProviderEventId.");
        if (amountRefundedMinor < 0)
            throw new DomainException("BillingRefundRecord amount cannot be negative.");

        var record = new BillingRefundRecord
        {
            Id = Guid.NewGuid(),
            SubscriptionId = subscriptionId,
            ProviderEventId = providerEventId,
            ChargeId = chargeId,
            InvoiceId = invoiceId,
            ChargeAmountMinor = chargeAmountMinor,
            AmountRefundedMinor = amountRefundedMinor,
            Currency = currency,
            Reason = reason,
            Kind = amountRefundedMinor >= chargeAmountMinor ? BillingRefundKind.Full : BillingRefundKind.Partial,
            OccurredAtUtc = occurredAtUtc,
            RecordedAtUtc = recordedAtUtc,
        };
        record.SetDealer(dealerId);
        return record;
    }
}
