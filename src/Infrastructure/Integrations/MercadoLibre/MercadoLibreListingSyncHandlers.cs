using System.Text.Json;
using Application.Abstractions.Data;
using Domain.Cars.Events;
using Domain.Integrations;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.Integrations.MercadoLibre;

/// <summary>
/// sfc C1.2 / ADR C25: reacts to the three Car triggers the design calls out — price change, sale,
/// deletion — by enqueuing a push <see cref="IntegrationCommand"/> for whatever MercadoLibre item
/// mirrors the car, if any. These handlers are published through the existing outbox pipeline
/// (<c>ApplicationDbContext.SaveChangesAsync</c> → <c>OutboxMessage</c> → <c>ProcessOutboxMessagesJob</c>
/// → <see cref="IPublisher.Publish"/>), exactly like every other domain-event handler in this
/// codebase — registered directly in <c>Infrastructure.DependencyInjection</c> because MediatR here
/// only scans the Application assembly for handlers, and this PR's allowed surface does not include it.
/// A car never published to MercadoLibre (no <see cref="ExternalListing"/> row) is a silent no-op.
/// </summary>
internal sealed class MercadoLibreListingSyncHandlers(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : INotificationHandler<CarPriceChangedDomainEvent>,
      INotificationHandler<CarSoldDomainEvent>,
      INotificationHandler<CarDeleteDomainEvent>
{
    /// <summary>RN-047 / ADR C25: a price edit pauses the listing — reversible, the dealer may republish later.</summary>
    public Task Handle(CarPriceChangedDomainEvent notification, CancellationToken cancellationToken) =>
        EnqueueAsync(notification.CarId, MercadoLibreClient.PauseListingOperation, cancellationToken);

    /// <summary>A sold car's listing is closed — MercadoLibre's API never supports a hard delete.</summary>
    public Task Handle(CarSoldDomainEvent notification, CancellationToken cancellationToken) =>
        EnqueueAsync(notification.CarId, MercadoLibreClient.CloseListingOperation, cancellationToken);

    /// <summary>A withdrawn (soft-deleted) car's listing is closed, same as a sale.</summary>
    public Task Handle(CarDeleteDomainEvent notification, CancellationToken cancellationToken) =>
        EnqueueAsync(notification.CarId, MercadoLibreClient.CloseListingOperation, cancellationToken);

    private async Task EnqueueAsync(Guid carId, string operation, CancellationToken cancellationToken)
    {
        ExternalListing? listing = await dbContext.ExternalListings
            .IgnoreQueryFilters()
            .Where(l => l.CarId == carId && l.Provider == MercadoLibreClient.ProviderKey && l.Status != ExternalListingStatus.Closed)
            .FirstOrDefaultAsync(cancellationToken);

        if (listing is null)
        {
            return;
        }

        string payload = JsonSerializer.Serialize(
            new MercadoLibreClient.ListingCommandPayload(listing.CarId, listing.ExternalId));

        dbContext.IntegrationCommands.Add(
            IntegrationCommand.Enqueue(listing.DealerId, MercadoLibreClient.ProviderKey, operation, payload, dateTimeProvider.UtcNow));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
