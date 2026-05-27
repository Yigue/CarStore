namespace Application.DealerSettings;

public sealed record DealerSettingsResponse
{
    public Guid Id { get; init; }
    public Guid DealerId { get; init; }
    public string DealerName { get; init; } = string.Empty;
    public string ContactEmail { get; init; } = string.Empty;
    public bool NotificationsEnabled { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string? HostName { get; init; }
    public string? CustomDomain { get; init; }
    public string? Address { get; init; }
    public string? PhoneNumber { get; init; }
    public string? FacebookUrl { get; init; }
    public string? InstagramUrl { get; init; }
    public string? TwitterUrl { get; init; }
    public decimal? InterestRateTna { get; init; }
}
