namespace Domain.Billing;

/// <summary>
/// spr ADR B9: persisted finding of the subscription reconciliation job — a dealer that has no
/// confirmed subscription (no row, or an abandoned <see cref="SubscriptionStatus.Incomplete"/> one)
/// past the grace window. It only marks; nothing is ever deleted or suspended automatically.
/// One row per dealer (PK = dealer id); a cleared flag can be reopened if the dealer regresses.
/// </summary>
public sealed class BillingReconciliationFlag
{
    public const string NoSubscriptionRow = "NoSubscriptionRow";
    public const string IncompleteAbandoned = "IncompleteAbandoned";

    private BillingReconciliationFlag()
    {
    }

    public Guid DealerId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime FlaggedAtUtc { get; private set; }
    public DateTime? ClearedAtUtc { get; private set; }

    public bool IsOpen => ClearedAtUtc is null;

    public static BillingReconciliationFlag Open(Guid dealerId, string reason, DateTime nowUtc) =>
        new() { DealerId = dealerId, Reason = reason, FlaggedAtUtc = nowUtc };

    /// <summary>Reopens a previously cleared flag.</summary>
    public void Reopen(string reason, DateTime nowUtc)
    {
        Reason = reason;
        FlaggedAtUtc = nowUtc;
        ClearedAtUtc = null;
    }

    public void Clear(DateTime nowUtc)
    {
        ClearedAtUtc ??= nowUtc;
    }
}
