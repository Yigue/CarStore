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
        // Validate car is available (only check ServiceCar, as CarStatus is about condition, not availability)
        if (car.ServiceCar != StatusServiceCar.Disponible)
        {
            return Result.Failure<Guid>(CarErrors.AlreadySold(command.CarId));
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
            dateTimeProvider.UtcNow
            );
 
        // Update car status using domain method
        car.MarkAsSold(dateTimeProvider.UtcNow);

        context.Sales.Add(sale);

        // Complete the sale to trigger financial transaction
        sale.Complete();

        // Single SaveChangesAsync: persists sale, car status, and all domain events in one transaction
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(sale.Id);
    }
}
