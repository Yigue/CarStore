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
            return Result.Failure<Guid>(SalesErrors.NotFound(command.CarId));
        }
        if (car.ServiceCar != statusServiceCar.Disponible)
        {
            return Result.Failure<Guid>(SalesErrors.AlreadySold(command.CarId));
        }

        // Verify if client exists
        Client? client = await context.Clients.FindAsync(new object[] { command.ClientId }, cancellationToken);
        if (client == null)
        {
            return Result.Failure<Guid>(SalesErrors.NotFound(command.ClientId));
        }

        var sale = new Sale(
            command.CarId,
            command.ClientId,
            command.FinalPrice,
            command.PaymentMethod,
            command.ContractNumber,
            command.Comments
            );

        sale.Raise(new SaleCreatedDomainEvent(sale.Id, command.CarId, command.ClientId, command.FinalPrice));

        // Update car status
        car.ServiceCar = statusServiceCar.Vendido;

        context.Sales.Add(sale);

        await context.SaveChangesAsync(cancellationToken);

        return sale.Id;
    }
}
