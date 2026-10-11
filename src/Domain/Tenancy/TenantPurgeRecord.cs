using System.Text.Json;
using SharedKernel;

namespace Domain.Tenancy;

/// <summary>
/// RN-013 (A1.4): tombstone kept after a tenant purge. Stores only the fiscal name snapshot and how many
/// rows were removed per aggregate, never the removed data itself. It survives the purge on purpose: it is
/// the evidence that the deletion happened and when.
/// </summary>
public sealed class TenantPurgeRecord : Entity
{
    private TenantPurgeRecord()
    {
    }

    public string DealerName { get; private set; } = string.Empty;
    public DateTime PurgedAtUtc { get; private set; }

    /// <summary>JSON object <c>{"cars":3,"clients":2,...}</c>.</summary>
    public string CountsJson { get; private set; } = "{}";

    public IReadOnlyDictionary<string, int> Counts =>
        JsonSerializer.Deserialize<Dictionary<string, int>>(CountsJson) ?? [];

    public static TenantPurgeRecord Create(
        Guid dealerId, string dealerName, DateTime purgedAtUtc, IReadOnlyDictionary<string, int> counts)
    {
        var record = new TenantPurgeRecord
        {
            Id = Guid.NewGuid(),
            DealerName = dealerName,
            PurgedAtUtc = purgedAtUtc,
            CountsJson = JsonSerializer.Serialize(counts)
        };
        record.SetDealer(dealerId);
        return record;
    }
}
