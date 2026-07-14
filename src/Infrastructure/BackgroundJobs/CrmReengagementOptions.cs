namespace Infrastructure.BackgroundJobs;

/// <summary>
/// Strongly-typed options bound from the <c>Crm:Reengagement</c> configuration section.
/// Consumed by <see cref="LeadReengagementJob"/>.
/// </summary>
public sealed class CrmReengagementOptions
{
    public const string SectionName = "Crm:Reengagement";

    /// <summary>
    /// Master switch. Defaults to <c>false</c> — no re-engagement email is ever sent
    /// until a dealer/operator explicitly opts in via configuration.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// A Perdido lead becomes eligible once this many days have passed. v1 approximation:
    /// measured from <c>Lead.CreatedAt</c> (the lead has no dedicated "became lost at"
    /// timestamp) — see the comment in <see cref="LeadReengagementJob"/>.
    /// </summary>
    public int DaysAfterLost { get; set; } = 30;
}
