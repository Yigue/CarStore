using SharedKernel;

namespace Domain.Integrations;

public enum ExternalListingStatus
{
    Active = 0,
    Paused = 1,
    Closed = 2
}

/// <summary>
/// sfc C1.2 / ADR C25: push-only mirror of a <c>Car</c> published to an external marketplace
/// (MercadoLibre first). CarStore remains the single source of truth — this row only remembers
/// which provider item represents the car and the last status pushed, so
/// <c>MercadoLibreListingSyncHandlers</c> knows which item to pause/close when the car's price
/// changes or it is sold/deleted, without a round trip to the provider just to look it up.
/// One row per (<see cref="Entity.DealerId"/>, <see cref="Provider"/>, <see cref="CarId"/>).
/// </summary>
public sealed class ExternalListing : Entity
{
    public Guid CarId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string ExternalId { get; private set; } = string.Empty;
    public ExternalListingStatus Status { get; private set; }
    public DateTime LastSyncedAtUtc { get; private set; }
    public string? LastError { get; private set; }

    private ExternalListing()
    {
    }

    public static ExternalListing Create(
        Guid dealerId, Guid carId, string provider, string externalId, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            throw new DomainException("ExternalId is required");
        }

        var listing = new ExternalListing
        {
            Id = Guid.NewGuid(),
            CarId = carId,
            Provider = DealerIntegration.NormalizeProvider(provider),
            ExternalId = externalId.Trim(),
            Status = ExternalListingStatus.Active,
            LastSyncedAtUtc = nowUtc,
        };
        listing.SetDealer(dealerId);
        return listing;
    }

    public void MarkPaused(DateTime nowUtc)
    {
        Status = ExternalListingStatus.Paused;
        LastSyncedAtUtc = nowUtc;
        LastError = null;
    }

    public void MarkClosed(DateTime nowUtc)
    {
        Status = ExternalListingStatus.Closed;
        LastSyncedAtUtc = nowUtc;
        LastError = null;
    }

    public void MarkSyncError(string error, DateTime nowUtc)
    {
        LastError = error.Length > 1000 ? error[..1000] : error;
        LastSyncedAtUtc = nowUtc;
    }
}
