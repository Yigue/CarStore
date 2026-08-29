using Application.Abstractions.Billing;
using Application.Abstractions.Messaging;
using Domain.Billing;
using SharedKernel;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Billing.Commands.HandleStripeWebhook;

internal sealed class HandleStripeWebhookCommandHandler : ICommandHandler<HandleStripeWebhookCommand>
{
    private readonly IDealerSubscriptionRepository _repository;

    public HandleStripeWebhookCommandHandler(IDealerSubscriptionRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(HandleStripeWebhookCommand request, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(request.RawJson);
        var root = doc.RootElement;
        var eventType = request.EventType;

        if (!root.TryGetProperty("data", out var dataElement) || 
            !dataElement.TryGetProperty("object", out var dataObject))
        {
            return Result.Failure(Error.Validation("Webhook.InvalidPayload", "Invalid event payload."));
        }

        switch (eventType)
        {
            case "customer.subscription.created":
            case "customer.subscription.updated":
                {
                    var customerId = dataObject.GetProperty("customer").GetString() ?? string.Empty;
                    var subscriptionId = dataObject.GetProperty("id").GetString() ?? string.Empty;
                    var status = dataObject.GetProperty("status").GetString() ?? string.Empty;
                    var currentPeriodStartUnix = dataObject.GetProperty("current_period_start").GetInt64();
                    var currentPeriodEndUnix = dataObject.GetProperty("current_period_end").GetInt64();

                    long? trialEndUnix = null;
                    if (dataObject.TryGetProperty("trial_end", out var trialEndProp) && trialEndProp.ValueKind != JsonValueKind.Null)
                    {
                        trialEndUnix = trialEndProp.GetInt64();
                    }

                    var priceId = string.Empty;
                    if (dataObject.TryGetProperty("items", out var itemsProp) && 
                        itemsProp.TryGetProperty("data", out var itemsDataProp) && 
                        itemsDataProp.ValueKind == JsonValueKind.Array && 
                        itemsDataProp.GetArrayLength() > 0)
                    {
                        var firstItem = itemsDataProp[0];
                        if (firstItem.TryGetProperty("price", out var priceProp))
                        {
                            priceId = priceProp.GetProperty("id").GetString() ?? string.Empty;
                        }
                    }

                    var periodStart = DateTimeOffset.FromUnixTimeSeconds(currentPeriodStartUnix).UtcDateTime;
                    var periodEnd = DateTimeOffset.FromUnixTimeSeconds(currentPeriodEndUnix).UtcDateTime;
                    var trialEndsAt = trialEndUnix.HasValue ? DateTimeOffset.FromUnixTimeSeconds(trialEndUnix.Value).UtcDateTime : (DateTime?)null;

                    var subscription = await _repository.GetByStripeCustomerIdAsync(customerId, cancellationToken);
                    if (subscription == null)
                    {
                        var dealerIdStr = string.Empty;
                        if (dataObject.TryGetProperty("metadata", out var metadataProp) && 
                            metadataProp.TryGetProperty("dealer_id", out var dealerIdProp))
                        {
                            dealerIdStr = dealerIdProp.GetString();
                        }

                        if (Guid.TryParse(dealerIdStr, out var dealerId))
                        {
                            subscription = DealerSubscription.Create(
                                dealerId,
                                customerId,
                                subscriptionId,
                                priceId,
                                trialEndsAt
                            );

                            subscription.RenewPeriod(periodStart, periodEnd);
                            UpdateAggregateStatus(subscription, status, subscriptionId, periodStart, periodEnd);
                            await _repository.AddAsync(subscription, cancellationToken);
                        }
                    }
                    else
                    {
                        subscription.RenewPeriod(periodStart, periodEnd);
                        UpdateAggregateStatus(subscription, status, subscriptionId, periodStart, periodEnd);
                        _repository.Update(subscription);
                    }
                    break;
                }

            case "invoice.payment_succeeded":
                {
                    var customerId = dataObject.GetProperty("customer").GetString() ?? string.Empty;
                    var subscriptionId = dataObject.GetProperty("subscription").GetString() ?? string.Empty;
                    var periodStartUnix = dataObject.GetProperty("period_start").GetInt64();
                    var periodEndUnix = dataObject.GetProperty("period_end").GetInt64();

                    var periodStart = DateTimeOffset.FromUnixTimeSeconds(periodStartUnix).UtcDateTime;
                    var periodEnd = DateTimeOffset.FromUnixTimeSeconds(periodEndUnix).UtcDateTime;

                    var subscription = await _repository.GetByStripeCustomerIdAsync(customerId, cancellationToken);
                    if (subscription != null)
                    {
                        subscription.RenewPeriod(periodStart, periodEnd);

                        if (subscription.Status == SubscriptionStatus.Suspended ||
                            subscription.Status == SubscriptionStatus.Cancelled ||
                            subscription.Status == SubscriptionStatus.PastDue)
                        {
                            subscription.Activate(subscriptionId, periodStart, periodEnd);
                        }
                        _repository.Update(subscription);
                    }
                    break;
                }

            case "invoice.payment_failed":
                {
                    var customerId = dataObject.GetProperty("customer").GetString() ?? string.Empty;
                    var subscription = await _repository.GetByStripeCustomerIdAsync(customerId, cancellationToken);
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
                        _repository.Update(subscription);
                    }
                    break;
                }

            case "customer.subscription.deleted":
                {
                    var customerId = dataObject.GetProperty("customer").GetString() ?? string.Empty;
                    var subscription = await _repository.GetByStripeCustomerIdAsync(customerId, cancellationToken);
                    if (subscription != null)
                    {
                        if (subscription.Status != SubscriptionStatus.Cancelled)
                        {
                            subscription.Suspend();
                        }
                        _repository.Update(subscription);
                    }
                    break;
                }
        }

        return Result.Success();
    }

    private static void UpdateAggregateStatus(DealerSubscription aggregate, string stripeStatus, string stripeSubscriptionId, DateTime start, DateTime end)
    {
        switch (stripeStatus)
        {
            case "active":
                if (aggregate.Status != SubscriptionStatus.Active)
                {
                    aggregate.Activate(stripeSubscriptionId, start, end);
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
