using Application.Abstractions.Messaging;
using Domain.DealerSettings.Events;
using MediatR;

namespace Application.Dealers.Provision;

/// <summary>
/// Handles <see cref="DealerProvisionedDomainEvent"/> by delegating to
/// <see cref="IDealerNotificationService"/> for the welcome email.
/// The email service is responsible for its own exception isolation
/// (D3 — SMTP failures must never roll back the tenant creation).
/// </summary>
public sealed class DealerProvisionedDomainEventHandler(
    IDealerNotificationService dealerNotificationService)
    : INotificationHandler<DealerProvisionedDomainEvent>
{
    public Task Handle(DealerProvisionedDomainEvent notification, CancellationToken cancellationToken)
    {
        return dealerNotificationService.SendProvisioningEmailAsync(
            notification.Subdomain,
            notification.DashboardUrl,
            notification.AdminEmail,
            cancellationToken);
    }
}