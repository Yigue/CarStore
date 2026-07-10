using Application.Abstractions.Billing;
using Domain.Billing.Events;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Billing.EventHandlers;

public sealed class SubscriptionCacheInvalidator :
    INotificationHandler<SubscriptionActivatedDomainEvent>,
    INotificationHandler<SubscriptionSuspendedDomainEvent>,
    INotificationHandler<SubscriptionCancelledDomainEvent>
{
    private readonly ISubscriptionStatusCache _cache;

    public SubscriptionCacheInvalidator(ISubscriptionStatusCache cache)
    {
        _cache = cache;
    }

    public Task Handle(SubscriptionActivatedDomainEvent notification, CancellationToken cancellationToken)
    {
        return _cache.InvalidateAsync(notification.DealerId, cancellationToken);
    }

    public Task Handle(SubscriptionSuspendedDomainEvent notification, CancellationToken cancellationToken)
    {
        return _cache.InvalidateAsync(notification.DealerId, cancellationToken);
    }

    public Task Handle(SubscriptionCancelledDomainEvent notification, CancellationToken cancellationToken)
    {
        return _cache.InvalidateAsync(notification.DealerId, cancellationToken);
    }
}
