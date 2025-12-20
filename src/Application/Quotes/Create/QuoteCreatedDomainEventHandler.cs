using Domain.Quotes.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Quotes.Create;

internal sealed class QuoteCreatedDomainEventHandler(
    ILogger<QuoteCreatedDomainEventHandler> logger)
    : INotificationHandler<QuoteCreatedDomainEvent>
{
    public Task Handle(QuoteCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Quote created: QuoteId={QuoteId}, CarId={CarId}, ClientId={ClientId}, ProposedPrice={ProposedPrice}",
            notification.QuoteId,
            notification.CarId,
            notification.ClientId,
            notification.ProposedPrice);

        // Por ahora solo logging, futuro: notificaciones, integraciones, etc.
        return Task.CompletedTask;
    }
}

