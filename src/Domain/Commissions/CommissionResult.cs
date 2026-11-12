using Domain.Shared.ValueObjects;

namespace Domain.Commissions;

/// <summary>Snapshot of a computed commission (ADR C5).</summary>
/// <param name="Base">Net price: final price minus trade-in, never negative.</param>
/// <param name="Rate">Percentage applied; null for a fixed rule or when unconfigured.</param>
/// <param name="Amount">Commission, rounded to 2 decimals (ToEven), in the sale currency.</param>
/// <param name="IsConfigured">False when no rule existed (RN-009: default 0%, shown as "sin configurar").</param>
public sealed record CommissionResult(Money Base, decimal? Rate, Money Amount, bool IsConfigured);
