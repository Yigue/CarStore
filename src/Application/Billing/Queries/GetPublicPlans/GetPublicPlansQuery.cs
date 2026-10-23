using Application.Abstractions.Messaging;
using System.Collections.Generic;

namespace Application.Billing.Queries.GetPublicPlans;

/// <summary>
/// spr PR 1.1 (SPR-02/BE-BILLING-07, ADR B1, D1): anonymous query backing <c>GET api/v1/billing/plans</c> —
/// the landing page reads the real Stripe price instead of a hardcoded one (D1: "landing = Stripe").
/// </summary>
public sealed record GetPublicPlansQuery : IQuery<IReadOnlyList<PublicPlanResponse>>;

public sealed record PublicPlanResponse(
    string Code,
    long AmountMinor,
    string Currency,
    string Interval,
    int TrialDays);
