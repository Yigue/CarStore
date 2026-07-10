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

    public async Task<SubscriptionStatus> GetStatusAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        var subscriptionService = new SubscriptionService(_stripeClient);
        var subscription = await subscriptionService.GetAsync(stripeSubscriptionId, cancellationToken: ct);
        return MapStripeStatus(subscription.Status);
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
