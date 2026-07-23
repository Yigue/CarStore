using Application.Abstractions.Billing;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Billing;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Billing.Queries.GetSubscriptionStatus;

internal sealed class GetSubscriptionStatusQueryHandler : IQueryHandler<GetSubscriptionStatusQuery, SubscriptionDto>
{
    private readonly IDealerSubscriptionRepository _repository;
    private readonly IApplicationDbContext _context;
    private readonly ISubscriptionGateway _subscriptionGateway;

    public GetSubscriptionStatusQueryHandler(
        IDealerSubscriptionRepository repository,
        IApplicationDbContext context,
        ISubscriptionGateway subscriptionGateway)
    {
        _repository = repository;
        _context = context;
        _subscriptionGateway = subscriptionGateway;
    }

    public async Task<Result<SubscriptionDto>> Handle(GetSubscriptionStatusQuery request, CancellationToken cancellationToken)
    {
        var subscription = await _repository.GetByDealerIdAsync(request.DealerId, cancellationToken);

        string status = subscription?.Status.ToString() ?? SubscriptionStatus.PastDue.ToString();
        DateTime? trialEndsAt = subscription?.TrialEndsAt ?? DateTime.UtcNow.AddDays(14);
        DateTime currentPeriodEnd = subscription?.CurrentPeriodEnd ?? DateTime.UtcNow.AddDays(14);
        string planName = "SaaS Monthly Plan";

        string reactivationUrl = string.Empty;
        if (subscription == null ||
            subscription.Status == SubscriptionStatus.Suspended ||
            subscription.Status == SubscriptionStatus.Cancelled ||
            subscription.Status == SubscriptionStatus.PastDue)
        {
            var adminUser = await _context.Users
                .Where(u => u.DealerId == request.DealerId && u.RoleId != Guid.Empty)
                .FirstOrDefaultAsync(cancellationToken);

            if (adminUser == null)
            {
                adminUser = await _context.Users
                    .Where(u => u.DealerId == request.DealerId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (adminUser != null)
            {
                try
                {
                    reactivationUrl = await _subscriptionGateway.CreateCheckoutSessionAsync(
                        request.DealerId,
                        adminUser.Email.Value,
                        cancellationToken);
                }
                catch
                {
                    reactivationUrl = "https://checkout.stripe.com/noop_session";
                }
            }
        }

        return Result.Success(new SubscriptionDto(
            status,
            trialEndsAt,
            currentPeriodEnd,
            reactivationUrl,
            planName));
    }
}
