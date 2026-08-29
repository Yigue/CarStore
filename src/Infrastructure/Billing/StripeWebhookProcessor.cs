using Application.Abstractions.Billing;
using Domain.Billing;
using Stripe;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Billing;

public static class StripeWebhookProcessor
{
    public static async Task HandleAsync(Event evt, IDealerSubscriptionRepository repo, CancellationToken ct = default)
    {
        switch (evt.Type)
        {
            case Events.CustomerSubscriptionCreated:
            case Events.CustomerSubscriptionUpdated:
                {
                    if (evt.Data.Object is Subscription stripeSub)
                    {
                        var subscription = await repo.GetByStripeCustomerIdAsync(stripeSub.CustomerId, ct);
                        if (subscription == null)
                        {
                            if (stripeSub.Metadata.TryGetValue("dealer_id", out var dealerIdStr) &&
                                Guid.TryParse(dealerIdStr, out var dealerId))
                            {
                                subscription = DealerSubscription.Create(
                                    dealerId,
                                    stripeSub.CustomerId,
                                    stripeSub.Id,
                                    stripeSub.Items.Data.FirstOrDefault()?.Price?.Id ?? string.Empty,
                                    stripeSub.TrialEnd
                                );

                                subscription.RenewPeriod(stripeSub.CurrentPeriodStart, stripeSub.CurrentPeriodEnd);
                                UpdateAggregateStatus(subscription, stripeSub);
                                await repo.AddAsync(subscription, ct);
                            }
                        }
                        else
                        {
                            subscription.RenewPeriod(stripeSub.CurrentPeriodStart, stripeSub.CurrentPeriodEnd);
                            UpdateAggregateStatus(subscription, stripeSub);
                            repo.Update(subscription);
                        }
                    }
                    break;
                }

            case Events.InvoicePaymentSucceeded:
                {
                    if (evt.Data.Object is Invoice invoice && !string.IsNullOrEmpty(invoice.CustomerId))
                    {
                        var subscription = await repo.GetByStripeCustomerIdAsync(invoice.CustomerId, ct);
                        if (subscription != null)
                        {
                            var periodStart = invoice.PeriodStart;
                            var periodEnd = invoice.PeriodEnd;

                            subscription.RenewPeriod(periodStart, periodEnd);

                            if (subscription.Status == SubscriptionStatus.Suspended ||
                                subscription.Status == SubscriptionStatus.Cancelled ||
                                subscription.Status == SubscriptionStatus.PastDue)
                            {
                                subscription.Activate(invoice.SubscriptionId, periodStart, periodEnd);
                            }
                            repo.Update(subscription);
                        }
                    }
                    break;
                }

            case Events.InvoicePaymentFailed:
                {
                    if (evt.Data.Object is Invoice invoice && !string.IsNullOrEmpty(invoice.CustomerId))
                    {
                        var subscription = await repo.GetByStripeCustomerIdAsync(invoice.CustomerId, ct);
                        if (subscription != null)
                        {
                            if (subscription.Status == SubscriptionStatus.PastDue)
                            {
                                subscription.Suspend();
                            }
                            else if (subscription.Status == SubscriptionStatus.Active || subscription.Status == SubscriptionStatus.Trialing)
                            {
                                subscription.MarkPastDue();
                            }
                            repo.Update(subscription);
                        }
                    }
                    break;
                }

            case Events.CustomerSubscriptionDeleted:
                {
                    if (evt.Data.Object is Subscription stripeSub)
                    {
                        var subscription = await repo.GetByStripeCustomerIdAsync(stripeSub.CustomerId, ct);
                        if (subscription != null)
                        {
                            if (subscription.Status != SubscriptionStatus.Cancelled)
                            {
                                subscription.Suspend();
                            }
                            repo.Update(subscription);
                        }
                    }
                    break;
                }
        }
    }

    private static void UpdateAggregateStatus(DealerSubscription aggregate, Subscription stripeSub)
    {
        switch (stripeSub.Status)
        {
            case "active":
                if (aggregate.Status != SubscriptionStatus.Active)
                {
                    aggregate.Activate(stripeSub.Id, stripeSub.CurrentPeriodStart, stripeSub.CurrentPeriodEnd);
                }
                break;
            case "trialing":
                break;
            case "past_due":
            case "incomplete":
                if (aggregate.Status != SubscriptionStatus.PastDue)
                {
                    aggregate.MarkPastDue();
                }
                break;
            case "unpaid":
                if (aggregate.Status != SubscriptionStatus.Suspended)
                {
                    aggregate.Suspend();
                }
                break;
            case "canceled":
            case "incomplete_expired":
                if (aggregate.Status != SubscriptionStatus.Cancelled)
                {
                    aggregate.Cancel();
                }
                break;
        }
    }
}
