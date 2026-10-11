using Application.Abstractions.Billing;
using Domain.Billing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Billing;

public sealed class StripeSubscriptionGateway : ISubscriptionGateway
{
    private readonly IStripeClient _stripeClient;
    private readonly IDealerSubscriptionRepository _repository;
    private readonly IConfiguration _configuration;
    private readonly StripeOptions _options;

    public StripeSubscriptionGateway(
        IStripeClient stripeClient,
        IDealerSubscriptionRepository repository,
        IConfiguration configuration,
        IOptions<StripeOptions> options)
    {
        _stripeClient = stripeClient;
        _repository = repository;
        _configuration = configuration;
        _options = options.Value;
    }

    public async Task<string> CreateCustomerAsync(Guid dealerId, string email, CancellationToken ct = default)
    {
        var existing = await _repository.GetByDealerIdAsync(dealerId, ct);
        if (existing != null && !string.IsNullOrWhiteSpace(existing.StripeCustomerId))
        {
            return existing.StripeCustomerId;
        }

        var customerService = new CustomerService(_stripeClient);
        var searchOptions = new CustomerSearchOptions
        {
            Query = $"metadata['dealer_id']:'{dealerId}'"
        };
        
        var searchResult = await customerService.SearchAsync(searchOptions, cancellationToken: ct);
        var existingStripeCustomer = searchResult.FirstOrDefault();
        if (existingStripeCustomer != null)
        {
            return existingStripeCustomer.Id;
        }

        var createOptions = new CustomerCreateOptions
        {
            Email = email,
            Metadata = new Dictionary<string, string>
            {
                { "dealer_id", dealerId.ToString() }
            }
        };

        var customer = await customerService.CreateAsync(createOptions, cancellationToken: ct);
        return customer.Id;
    }

    public async Task<string> CreateCheckoutSessionAsync(Guid dealerId, string dealerEmail, CancellationToken ct = default)
    {
        string stripeCustomerId = await CreateCustomerAsync(dealerId, dealerEmail, ct);

        var sessionService = new SessionService(_stripeClient);
        string baseUrl = (_configuration["Frontend:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/');

        var options = new SessionCreateOptions
        {
            Customer = stripeCustomerId,
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    Price = _options.PriceId,
                    Quantity = 1
                }
            },
            Mode = "subscription",
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                TrialPeriodDays = _options.TrialDays > 0 ? _options.TrialDays : null,
                Metadata = new Dictionary<string, string>
                {
                    { "dealer_id", dealerId.ToString() }
                }
            },
            Metadata = new Dictionary<string, string>
            {
                { "dealer_id", dealerId.ToString() }
            },
            SuccessUrl = $"{baseUrl}/dashboard?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{baseUrl}/dashboard"
        };

        var session = await sessionService.CreateAsync(options, cancellationToken: ct);
        return session.Url;
    }

    public async Task CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        var subscriptionService = new SubscriptionService(_stripeClient);
        var options = new SubscriptionUpdateOptions
        {
            CancelAtPeriodEnd = true
        };
        await subscriptionService.UpdateAsync(stripeSubscriptionId, options, cancellationToken: ct);
    }

    public async Task CancelNowAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        var subscriptionService = new SubscriptionService(_stripeClient);
        try
        {
            await subscriptionService.CancelAsync(
                stripeSubscriptionId,
                new SubscriptionCancelOptions { InvoiceNow = false, Prorate = false },
                cancellationToken: ct);
        }
        catch (StripeException ex) when (ex.StripeError?.Code == "resource_missing")
        {
            // Already cancelled / gone at the provider: the end state is the one we wanted.
        }
    }

    public async Task<SubscriptionStatus> GetStatusAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        var subscriptionService = new SubscriptionService(_stripeClient);
        var subscription = await subscriptionService.GetAsync(stripeSubscriptionId, cancellationToken: ct);
        return MapStripeStatus(subscription.Status);
    }

    public async Task<ExternalSubscription?> FindSubscriptionForDealerAsync(Guid dealerId, CancellationToken ct = default)
    {
        var subscriptionService = new SubscriptionService(_stripeClient);
        var search = await subscriptionService.SearchAsync(
            new SubscriptionSearchOptions { Query = $"metadata['dealer_id']:'{dealerId}'", Limit = 10 },
            cancellationToken: ct);

        // Most recent live subscription wins; a cancelled one does not count as "confirmed".
        var live = search.Data
            .Where(s => s.Status != "canceled" && s.Status != "incomplete_expired")
            .OrderByDescending(s => s.Created)
            .FirstOrDefault();

        if (live is null)
        {
            return null;
        }

        return new ExternalSubscription(
            live.CustomerId,
            live.Id,
            MapStripeStatus(live.Status),
            live.CurrentPeriodStart,
            live.CurrentPeriodEnd,
            live.TrialEnd,
            live.Items?.Data?.FirstOrDefault()?.Price?.Id);
    }

    private static SubscriptionStatus MapStripeStatus(string stripeStatus)
    {
        return stripeStatus switch
        {
            "active" => SubscriptionStatus.Active,
            "trialing" => SubscriptionStatus.Trialing,
            "past_due" => SubscriptionStatus.PastDue,
            "unpaid" => SubscriptionStatus.Suspended,
            "canceled" => SubscriptionStatus.Cancelled,
            "incomplete" => SubscriptionStatus.PastDue,
            "incomplete_expired" => SubscriptionStatus.Cancelled,
            _ => SubscriptionStatus.Suspended
        };
    }
}
