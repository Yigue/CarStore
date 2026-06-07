namespace Domain.Cars;

/// <summary>
/// Discriminator for <see cref="BackfillAudit"/> rows.
/// Persisted as <c>VARCHAR(16)</c> via EF Core string conversion — the string values
/// match the C# identifiers so that audit rows are readable directly from the DB.
/// </summary>
public enum BackfillAction
{
    DryRun = 0,
    Apply = 1,
}
