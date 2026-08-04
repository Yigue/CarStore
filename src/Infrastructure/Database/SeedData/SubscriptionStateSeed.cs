using System;
using System.Collections.Generic;
using Domain.Billing;

namespace Infrastructure.Database.SeedData;

public record SubscriptionStateSeedItem(
    Guid DealerId,
    string Email,
    string DealerName,
    SubscriptionStatus Status);

public static class SubscriptionStateSeed
{
    public static readonly IReadOnlyList<SubscriptionStateSeedItem> AdditionalDealers = new[]
    {
        new SubscriptionStateSeedItem(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "admin.trialing@dealer.com",
            "Trialing Dealer",
            SubscriptionStatus.Trialing),

        new SubscriptionStateSeedItem(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "admin.pastdue@dealer.com",
            "PastDue Dealer",
            SubscriptionStatus.PastDue),

        new SubscriptionStateSeedItem(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            "admin.suspended@dealer.com",
            "Suspended Dealer",
            SubscriptionStatus.Suspended),

        new SubscriptionStateSeedItem(
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            "admin.cancelled@dealer.com",
            "Cancelled Dealer",
            SubscriptionStatus.Cancelled)
    };

    public static DealerSubscription CreateSeededSubscription(Guid dealerId, SubscriptionStatus status)
    {
        var now = DateTime.UtcNow;
        var sub = DealerSubscription.Create(dealerId, $"cus_{dealerId:N}", $"sub_{dealerId:N}", "plan_mock");
        switch (status)
        {
            case SubscriptionStatus.Active:
                sub.Activate($"sub_{dealerId:N}", now.AddDays(-1), now.AddDays(30));
                break;
            case SubscriptionStatus.PastDue:
                sub.Activate($"sub_{dealerId:N}", now.AddDays(-1), now.AddDays(30));
                sub.MarkPastDue();
                break;
            case SubscriptionStatus.Suspended:
                sub.Activate($"sub_{dealerId:N}", now.AddDays(-1), now.AddDays(30));
                sub.Suspend();
                break;
            case SubscriptionStatus.Cancelled:
                sub.Cancel();
                break;
            case SubscriptionStatus.Trialing:
                // Already Trialing by default on Create
                break;
        }
        return sub;
    }
}
