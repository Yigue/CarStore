using Domain.DealerSettings.Events;
using SharedKernel;

namespace Domain.DealerSettings;

/// <summary>
/// Configuración persistente de una concesionaria. Una fila por DealerId.
/// Pensado para que el dueño/admin del dealer ajuste preferencias generales
/// desde la pantalla de Configuración del dashboard.
/// </summary>
public sealed class DealerSettings : Entity
{
    // Required by EF Core
    private DealerSettings()
    {
    }

    public DealerSettings(
        Guid dealerId,
        string dealerName,
        string contactEmail,
        bool notificationsEnabled = true,
        string? hostName = null,
        string? customDomain = null,
        string? address = null,
        string? phoneNumber = null,
        string? facebookUrl = null,
        string? instagramUrl = null,
        string? twitterUrl = null,
        decimal? interestRateTna = null)
    {
        SetDealer(dealerId);
        Id = Guid.NewGuid();
        DealerName = dealerName;
        ContactEmail = contactEmail;
        NotificationsEnabled = notificationsEnabled;
        HostName = hostName;
        CustomDomain = customDomain;
        Address = address;
        PhoneNumber = phoneNumber;
        FacebookUrl = facebookUrl;
        InstagramUrl = instagramUrl;
        TwitterUrl = twitterUrl;
        InterestRateTna = interestRateTna;
        LastAssignedAgentIndex = 0;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Provisioning ctor — mints the row PK and tenant FK to the SAME
    /// <paramref name="id"/> per design ADR-1 + the orchestrator locked decision.
    /// Used by <c>ProvisionDealerCommandHandler</c> only; the legacy ctor above
    /// keeps its independent <c>Guid.NewGuid()</c> semantics for backfill / seed paths.
    /// </summary>
    public DealerSettings(
        Guid id,
        Guid dealerId,
        string dealerName,
        string contactEmail,
        bool notificationsEnabled = true,
        string? hostName = null,
        string? customDomain = null,
        string? address = null,
        string? phoneNumber = null,
        string? facebookUrl = null,
        string? instagramUrl = null,
        string? twitterUrl = null,
        decimal? interestRateTna = null)
    {
        if (id != dealerId)
        {
            throw new DomainException(
                "Provisioning requires DealerSettings.Id to equal DealerSettings.DealerId.");
        }

        SetDealer(dealerId);
        Id = id;
        DealerName = dealerName;
        ContactEmail = contactEmail;
        NotificationsEnabled = notificationsEnabled;
        HostName = hostName;
        CustomDomain = customDomain;
        Address = address;
        PhoneNumber = phoneNumber;
        FacebookUrl = facebookUrl;
        InstagramUrl = instagramUrl;
        TwitterUrl = twitterUrl;
        InterestRateTna = interestRateTna;
        LastAssignedAgentIndex = 0;
        UpdatedAt = DateTime.UtcNow;
    }

    public string DealerName { get; private set; }

    public string ContactEmail { get; private set; }

    public bool NotificationsEnabled { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Subdomain slug that the tenant middleware resolves against
    /// <c>Host</c>/<c>X-Tenant-Host</c>. Enforced unique at the DB level
    /// (partial unique index — null values are ignored for backward compat with
    /// legacy seed rows). Always stored lowercase.
    /// </summary>
    public string? HostName { get; private set; }

    public string? CustomDomain { get; private set; }

    public string? Address { get; private set; }

    public string? PhoneNumber { get; private set; }

    public string? FacebookUrl { get; private set; }

    public string? InstagramUrl { get; private set; }

    public string? TwitterUrl { get; private set; }

    public decimal? InterestRateTna { get; private set; }

    // Visual settings
    public string? LogoUrl { get; private set; }
    public string? PrimaryColor { get; private set; }
    public string? SecondaryColor { get; private set; }
    public string? FooterText { get; private set; }

    // Platform suspension
    public bool IsActive { get; private set; } = true;
    public DateTime? SuspendedAt { get; private set; }
    public string? SuspendReason { get; private set; }

    /// <summary>
    /// Postgres xmin concurrency token. Populated by EF Core (Npgsql) from the
    /// xmin system column. Zero on non-Postgres providers (InMemory/SQLite).
    /// </summary>
    public uint RowVersion { get; internal set; }

    /// <summary>
    /// Puntero round-robin para asignación automática de leads a agentes.
    /// Incrementa atómicamente vía <see cref="IncrementAgentIndex"/>.
    /// </summary>
    public int LastAssignedAgentIndex { get; private set; }

    /// <summary>
    /// Incrementa el puntero round-robin y lo retorna para uso del allocator.
    /// </summary>
    public int IncrementAgentIndex()
    {
        LastAssignedAgentIndex += 1;
        return LastAssignedAgentIndex;
    }

    public void UpdateVisual(string? logoUrl, string? primaryColor, string? secondaryColor, string? footerText)
    {
        if (primaryColor != null && !string.IsNullOrWhiteSpace(primaryColor) && !System.Text.RegularExpressions.Regex.IsMatch(primaryColor, @"^#[0-9A-Fa-f]{6}$"))
            throw new DomainException("PrimaryColor must be a valid hex color (e.g., #FF0000)");

        if (secondaryColor != null && !string.IsNullOrWhiteSpace(secondaryColor) && !System.Text.RegularExpressions.Regex.IsMatch(secondaryColor, @"^#[0-9A-Fa-f]{6}$"))
            throw new DomainException("SecondaryColor must be a valid hex color (e.g., #00FF00)");

        LogoUrl = logoUrl;
        PrimaryColor = primaryColor;
        SecondaryColor = secondaryColor;
        FooterText = footerText;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(
        string dealerName,
        string contactEmail,
        bool notificationsEnabled,
        string? hostName,
        string? customDomain,
        string? address,
        string? phoneNumber,
        string? facebookUrl,
        string? instagramUrl,
        string? twitterUrl,
        decimal? interestRateTna)
    {
        if (string.IsNullOrWhiteSpace(dealerName))
            throw new DomainException("DealerName cannot be empty");

        if (string.IsNullOrWhiteSpace(contactEmail))
            throw new DomainException("ContactEmail cannot be empty");

        DealerName = dealerName;
        ContactEmail = contactEmail;
        NotificationsEnabled = notificationsEnabled;
        HostName = hostName;
        CustomDomain = customDomain;
        Address = address;
        PhoneNumber = phoneNumber;
        FacebookUrl = facebookUrl;
        InstagramUrl = instagramUrl;
        TwitterUrl = twitterUrl;
        InterestRateTna = interestRateTna;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Soft-suspends the dealer. Idempotent: calling on an already-suspended dealer is a no-op.
    /// </summary>
    public void Suspend(string reason, Guid actorId, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("SuspendReason cannot be empty");

        if (!IsActive)
            return; // idempotent

        IsActive = false;
        SuspendedAt = utcNow;
        SuspendReason = reason;
        UpdatedAt = utcNow;

        Raise(new DealerSuspendedDomainEvent(Id, reason, actorId, utcNow));
    }

    /// <summary>
    /// Reactivates the dealer. Idempotent: calling on an already-active dealer is a no-op.
    /// </summary>
    public void Activate()
    {
        if (IsActive)
            return; // idempotent

        var reactivatedAt = DateTime.UtcNow;
        IsActive = true;
        SuspendedAt = null;
        SuspendReason = null;
        UpdatedAt = reactivatedAt;

        Raise(new DealerReactivatedDomainEvent(Id, reactivatedAt));
    }
}
