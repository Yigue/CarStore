using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Clients;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Sales;
using Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Financial.Create;

internal sealed class CreateFinancialCommandHandler(
    IApplicationDbContext context,
    CachedCategoryService cachedCategoryService)
    : ICommandHandler<CreateFinancialCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateFinancialCommand command, CancellationToken cancellationToken)
    {
        Car? car = null;
        Client? client = null;
        Sale? sale = null;
       
        if(command.CarId != null)
        {
            car = await context.Cars
                .SingleOrDefaultAsync(c => c.Id == command.CarId, cancellationToken);

            if (car is null)
            {
                return Result.Failure<Guid>(FinancialErrors.AttributesInvalid());
            }
        }
        if(command.ClientId != null)
        {
            client = await context.Clients
                .SingleOrDefaultAsync(c => c.Id == command.ClientId, cancellationToken);

            if (client is null)
            {
                return Result.Failure<Guid>(FinancialErrors.AttributesInvalid());
            }
        }
        if(command.SaleId != null)
        {
            sale = await context.Sales
                .SingleOrDefaultAsync(c => c.Id == command.SaleId, cancellationToken);

            if (sale is null)
            {
                return Result.Failure<Guid>(FinancialErrors.AttributesInvalid());
            }
        }
        
        // Usar servicio de caché para obtener categoría
        TransactionCategory? category = await cachedCategoryService.GetByIdAsync(command.CategoryId, cancellationToken);

        if (category is null)
        {
            return Result.Failure<Guid>(FinancialErrors.AttributesInvalid());
        }

        var financial = new FinancialTransaction(
            command.Type,
            command.Amount,
            command.Description,
            command.PaymentMethod,
            category,
            car,
            client,
            sale);

        context.Transactions.Add(financial);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(financial.Id);
    }
}
