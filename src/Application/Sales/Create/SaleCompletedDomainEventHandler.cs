using Application.Abstractions.Data;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Sales.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sales.Create;

internal sealed class SaleCompletedDomainEventHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : INotificationHandler<SaleCompletedDomainEvent>
{
    public async Task Handle(SaleCompletedDomainEvent notification, CancellationToken cancellationToken)
    {
        // Buscar categoría "Venta de Auto" (o crear si no existe)
        var category = await context.TransactionCategories
            .FirstOrDefaultAsync(c => c.Name == "Venta de Auto", cancellationToken);
        
        if (category == null)
        {
            // Crear categoría si no existe
            category = new TransactionCategory(
                "Venta de Auto",
                "Ingresos por venta de vehículos",
                TransactionType.Income);
            context.TransactionCategories.Add(category);
            await context.SaveChangesAsync(cancellationToken);
        }
        
        // Buscar la venta para obtener referencias
        var sale = await context.Sales
            .Include(s => s.Car)
            .Include(s => s.Client)
            .FirstOrDefaultAsync(s => s.Id == notification.SaleId, cancellationToken);
        
        if (sale == null)
        {
            // Log error pero no lanzar excepción para no romper la transacción
            return;
        }
        
        var transaction = new FinancialTransaction(
            TransactionType.Income,
            notification.FinalPrice,
            $"Venta de auto - Contrato: {sale.ContractNumber}",
            notification.PaymentMethod,
            category,
            car: sale.Car,
            client: sale.Client,
            sale: sale);
        
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync(cancellationToken);
    }
}

