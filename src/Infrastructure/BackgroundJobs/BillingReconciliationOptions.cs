namespace Infrastructure.BackgroundJobs;

/// <summary>spr ADR B9: <c>Billing:Reconciliation</c> section.</summary>
public sealed class BillingReconciliationOptions
{
    public const string SectionName = "Billing:Reconciliation";

    public bool Enabled { get; set; } = true;

    public int IntervalMinutes { get; set; } = 60;

    /// <summary>A dealer without a confirmed subscription is only a finding after this many hours.</summary>
    public int GraceHours { get; set; } = 48;

    public int BatchSize { get; set; } = 50;
}
