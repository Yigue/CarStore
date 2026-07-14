using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Cars.Events;
using Domain.Clients;
using Domain.Clients.Attributes;
using Domain.Quotes;
using Domain.Sales;
using Domain.Sales.Attributes;
using Domain.Sales.Events;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sales.Create;

internal sealed class CreateSaleCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    ICurrentTenantService tenantService)
    : ICommandHandler<CreateSaleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateSaleCommand command, CancellationToken cancellationToken)
    {
        // Verify if car exists and is available
        Car? car = await context.Cars
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == command.CarId, cancellationToken);      
        if (car == null)
        {
            return Result.Failure<Guid>(CarErrors.NotFound(command.CarId));
        }        
        // Validate car is available (only check ServiceCar, as CarStatus is about condition, not availability).
        // D-1: un vehículo Reservado (tomado por la cotización que se está convirtiendo) también es vendible.
        if (car.ServiceCar != StatusServiceCar.Disponible && car.ServiceCar != StatusServiceCar.Reservado)
        {
            return Result.Failure<Guid>(CarErrors.AlreadySold(command.CarId));
        }

        // D-5: a quote converts into at most one sale. Guard against a second sale created
        // from the same quote (idempotency on the quote -> sale conversion). Sales are
        // tenant-scoped, so the default query filter keeps this within the dealer.
        if (command.QuoteId is { } quoteId)
        {
            bool alreadyConverted = await context.Sales
                .AnyAsync(s => s.QuoteId == quoteId, cancellationToken);

            if (alreadyConverted)
            {
                return Result.Failure<Guid>(SalesErrors.AlreadyConvertedFromQuote(quoteId));
            }
        }

        // Verify if client exists
        Client? client = await context.Clients.FindAsync(new object[] { command.ClientId }, cancellationToken);
        if (client == null)
        {
            return Result.Failure<Guid>(ClientErrors.NotFound(command.ClientId));
        }
        
        // Validate client is active
        if (client.Status != ClientStatus.Active)
        {
            return Result.Failure<Guid>(ClientErrors.Inactive(client.Id));
        }
 
        var sale = new Sale(
            tenantService.DealerId,
            command.CarId,
            command.ClientId,
            command.FinalPrice,
            command.PaymentMethod,
            command.ContractNumber,
            command.Comments,
            dateTimeProvider.UtcNow,
            command.LeadId,
            command.QuoteId,
            command.SalespersonId
            );

        // Sales are Pending by default. They are only force-completed when the
        // caller explicitly requests it (e.g. a legacy/cash sale recorded as
        // already settled). Cancelled as an initial status is rejected by the validator.
        SaleStatus requestedStatus = command.Status ?? SaleStatus.Pending;

        if (requestedStatus == SaleStatus.Completed)
        {
            // Update car status using domain method
            car.MarkAsSold(dateTimeProvider.UtcNow);

            context.Sales.Add(sale);

            // Complete the sale to trigger financial transaction
            sale.Complete();
        }
        else
        {
            context.Sales.Add(sale);

            // Pending sale: reserve the car instead of marking it sold outright.
            // If it's already Reservado (e.g. converted from an accepted quote,
            // see D-1), leave it as-is — Reserve() would otherwise throw.
            if (car.ServiceCar == StatusServiceCar.Disponible)
            {
                car.Reserve(dateTimeProvider.UtcNow);
            }
        }

        // Single SaveChangesAsync: persists sale, car status, and all domain events in one transaction
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(sale.Id);
    }
}
