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
        UpdatedAt = DateTime.UtcNow;
    }

    public string DealerName { get; private set; }

    public string ContactEmail { get; private set; }

    public bool NotificationsEnabled { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public string? HostName { get; private set; }

    public string? CustomDomain { get; private set; }

    public string? Address { get; private set; }

    public string? PhoneNumber { get; private set; }

    public string? FacebookUrl { get; private set; }

    public string? InstagramUrl { get; private set; }

    public string? TwitterUrl { get; private set; }

    public decimal? InterestRateTna { get; private set; }

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
}
