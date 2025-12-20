using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Cars.Atribbutes;
using Domain.Cars.Events;
using Domain.Clients;
using Domain.Quotes;
using Domain.Sales;
using Domain.Sales.Events;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sales.Create;

internal sealed class CreateSaleCommandHandler(
    IApplicationDbContext context)
    : ICommandHandler<CreateSaleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateSaleCommand command, CancellationToken cancellationToken)
    {
        // Verify if car exists and is available
        Car? car = await context.Cars.FindAsync(new object[] { command.CarId }, cancellationToken);
        if (car == null)
        {
            return Result.Failure<Guid>(CarErrors.NotFound(command.CarId));
        }
        
        // Validate car is available (only check ServiceCar, as CarStatus is about condition, not availability)
        if (car.ServiceCar != statusServiceCar.Disponible)
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
            command.CarId,
            command.ClientId,
            command.FinalPrice,
            command.PaymentMethod,
            command.ContractNumber,
            command.Comments
            );

        // Update car status using domain method
        car.MarkAsSold();

        context.Sales.Add(sale);

        await context.SaveChangesAsync(cancellationToken);

        // Complete the sale to trigger financial transaction
        sale.Complete();

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(sale.Id);
    }
}
