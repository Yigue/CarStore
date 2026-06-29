using SharedKernel;

namespace Domain.DealerSettings.Events;

/// <summary>
/// Raised by <c>ProvisionDealerCommandHandler</c> after the new
/// <see cref="Domain.DealerSettings.DealerSettings"/> row and its first Admin
/// <c>User</c> are committed. Handlers send the dealer provisioning welcome email
/// and any other post-provisioning side effects.
/// </summary>
public sealed record DealerProvisionedDomainEvent(
    Guid DealerId,
    Guid AdminUserId,
    string Subdomain,
    string DashboardUrl) : IDomainEvent;