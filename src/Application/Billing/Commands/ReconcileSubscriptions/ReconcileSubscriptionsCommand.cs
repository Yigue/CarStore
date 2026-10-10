using Application.Abstractions.Messaging;

namespace Application.Billing.Commands.ReconcileSubscriptions;

/// <param name="Grace">How long a dealer may stay without a confirmed subscription before it is a finding.</param>
/// <param name="BatchSize">Maximum dealers examined per run (the job runs periodically).</param>
public sealed record ReconcileSubscriptionsCommand(TimeSpan Grace, int BatchSize = 50)
    : ICommand<ReconcileSubscriptionsResult>;

public sealed record ReconcileSubscriptionsResult(int Examined, int Healed, int Flagged, int Cleared);
