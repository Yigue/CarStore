namespace Application.Dealers.CheckSubdomain;

/// <summary>
/// Response payload for <see cref="CheckSubdomainAvailabilityQuery"/>.
/// </summary>
public sealed record SubdomainAvailabilityResponse(
    bool Available,
    string? Reason,
    bool Reserved);