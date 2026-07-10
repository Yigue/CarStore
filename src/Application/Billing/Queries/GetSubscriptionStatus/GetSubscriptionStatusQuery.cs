using Application.Abstractions.Messaging;
using System;

namespace Application.Billing.Queries.GetSubscriptionStatus;

public sealed record GetSubscriptionStatusQuery(Guid DealerId) : IQuery<SubscriptionDto>;

public sealed record SubscriptionDto(
    string Status,
    DateTime? TrialEndsAt,
    DateTime CurrentPeriodEnd,
    string ReactivationUrl,
    string PlanName);
