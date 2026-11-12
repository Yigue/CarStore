using Domain.Billing;

namespace Application.Platform.Common;

/// <summary>
/// spr PR 2.1 (BE-PLATFORM-06/07, D3): the trailing 5 fields are additive — appended after the
/// original 10, never inserted, so no positional constructor call site breaks.
/// <see cref="SubscriptionStatus"/>/<see cref="PlanCode"/>/<see cref="TrialEndsAt"/>/<see cref="NextChargeAt"/>
/// are null when the dealer has no <c>DealerSubscription</c> row (legacy / pre-checkout).
/// <see cref="OperationalStatus"/> is always populated via <see cref="DealerOperationalStatusResolver"/>.
/// </summary>
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
    string ETag,
    SubscriptionStatus? SubscriptionStatus,
    string? PlanCode,
    DateTime? TrialEndsAt,
    DateTime? NextChargeAt,
    DealerOperationalStatus OperationalStatus);
