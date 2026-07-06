namespace Application.Platform.Common;

public sealed record PlatformDealerResponse(
    Guid Id,
    Guid DealerId,
    string DealerName,
    string ContactEmail,
    bool IsActive,
    DateTime? SuspendedAt,
    string? SuspendReason,
    DateTime CreatedAt,
    string? CustomDomain,
    string ETag);
