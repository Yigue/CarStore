using Application.Abstractions.Messaging;

namespace Application.Deposits.Expire;

/// <summary>
/// sfc B1.3.3 / RN-002 / RN-003: the platform-wide deposit sweep, run hourly by <c>ExpireDepositsJob</c> with no tenant.
/// Expires due deposits, releases their cars when nothing else holds them, and sends the 24 h expiring notice once.
/// </summary>
public sealed record ExpireDueDepositsCommand : ICommand<DepositExpiryRunSummary>;

public sealed record DepositExpiryRunSummary(int Expired, int CarsReleased, int NoticesSent);
