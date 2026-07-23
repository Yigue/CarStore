using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Billing.Events;
using Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Billing.EventHandlers;

public sealed class SendPaymentFailedEmailHandler : INotificationHandler<SubscriptionPaymentFailedDomainEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;

    public SendPaymentFailedEmailHandler(IApplicationDbContext context, IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    public async Task Handle(SubscriptionPaymentFailedDomainEvent notification, CancellationToken cancellationToken)
    {
        var adminUser = await _context.Users
            .Where(u => u.DealerId == notification.DealerId && u.RoleId != Guid.Empty)
            .FirstOrDefaultAsync(cancellationToken);

        if (adminUser == null)
        {
            adminUser = await _context.Users
                .Where(u => u.DealerId == notification.DealerId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (adminUser != null)
        {
            string toEmail = adminUser.Email.Value;
            string subject = "Subscription Payment Failed";
            string body = $"Dear {adminUser.FirstName},\n\nThe payment for your dealership subscription has failed. Please update your payment method to avoid service interruption.";
            await _emailService.SendEmailAsync(toEmail, subject, body, cancellationToken);
        }
    }
}
