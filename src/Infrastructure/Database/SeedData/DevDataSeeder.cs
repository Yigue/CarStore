using Application.Abstractions.Data;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Quotes;
using Domain.Sales;
using Domain.Sales.Attributes;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Database.SeedData;

/// <summary>
/// Seeder para datos de prueba en desarrollo.
/// </summary>
internal static class DevDataSeeder
{
    private static readonly Guid DefaultDealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static async Task SeedAsync(IApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        // 1. Clientes
        if (!await context.Clients.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            var clients = new List<Client>
            {
                new Client(DefaultDealerId, "Juan", "Perez", "30123456", "juan.perez@email.com", "+54 11 1234-5678", "Av. Corrientes 1234, CABA", DateTime.UtcNow),
                new Client(DefaultDealerId, "Maria", "Gonzalez", "28765432", "maria.gonzalez@email.com", "+54 11 5555-1234", "Calle Florida 123, CABA", DateTime.UtcNow),
                new Client(DefaultDealerId, "Carlos", "Rodriguez", "35111222", "carlos.rod@email.com", "+54 11 4444-0000", "Av. Santa Fe 4567, CABA", DateTime.UtcNow)
            };

            context.Clients.AddRange(clients);
            await context.SaveChangesAsync(cancellationToken);
        }

        // 2. Autos
        if (!await context.Cars.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            var marcas = await context.Marca.ToListAsync(cancellationToken);
            var modelos = await context.Modelo.ToListAsync(cancellationToken);

            var toyota = marcas.First(m => m.Nombre == "Toyota");
            var corolla = modelos.First(m => m.Nombre == "Corolla");
            var hilux = modelos.First(m => m.Nombre == "Hilux");

            var ford = marcas.First(m => m.Nombre == "Ford");
            var ranger = modelos.First(m => m.Nombre == "Ranger");

            var cars = new List<Car>
            {
                new Car(DefaultDealerId, toyota, corolla, Color.White, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 1800, 0, 2024, "AB123CD", "Toyota Corolla 2024 - Nuevo", 25000, DateTime.UtcNow),
                new Car(DefaultDealerId, toyota, hilux, Color.Gray, TypeCar.Pickup, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 2800, 0, 2024, "AB456EF", "Toyota Hilux 2024 - Nueva", 45000, DateTime.UtcNow),
                new Car(DefaultDealerId, ford, ranger, Color.Black, TypeCar.Pickup, StatusCar.Used, StatusServiceCar.Disponible, 4, 5, 3200, 45000, 2022, "AF789GH", "Ford Ranger 2022 - Usada", 32000, DateTime.UtcNow),
                new Car(DefaultDealerId, toyota, corolla, Color.Silver, TypeCar.Sedan, StatusCar.Certified, StatusServiceCar.EnVenta, 4, 5, 1800, 15000, 2023, "AE012IJ", "Toyota Corolla 2023 - Certificado", 22000, DateTime.UtcNow),
                new Car(DefaultDealerId, ford, ranger, Color.Red, TypeCar.Pickup, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 2000, 0, 2024, "AG345KL", "Ford Ranger 2024 - Raptor", 65000, DateTime.UtcNow)
            };

            context.Cars.AddRange(cars);
            await context.SaveChangesAsync(cancellationToken);
        }

        // 3. Ventas
        if (!await context.Sales.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            var cars = await context.Cars.ToListAsync(cancellationToken);
            var clients = await context.Clients.ToListAsync(cancellationToken);

            var sale1 = new Sale(DefaultDealerId, cars[0].Id, clients[0].Id, 24500, PaymentMethod.Cash, "CNT-2024-001", "Venta en efectivo", DateTime.UtcNow.AddDays(-5));
            sale1.Complete();

            var sale2 = new Sale(DefaultDealerId, cars[1].Id, clients[1].Id, 44000, PaymentMethod.BankTransfer, "CNT-2024-002", "Venta pendiente de pago", DateTime.UtcNow.AddDays(-2));

            var sale3 = new Sale(DefaultDealerId, cars[2].Id, clients[2].Id, 31000, PaymentMethod.CreditCard, "CNT-2024-003", "Venta cancelada por el cliente", DateTime.UtcNow.AddDays(-10));
            sale3.Cancel("El cliente desistió de la compra por falta de financiamiento.");

            context.Sales.AddRange(new[] { sale1, sale2, sale3 });
            await context.SaveChangesAsync(cancellationToken);
        }

        // 4. Cotizaciones
        if (!await context.Quotes.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            var cars = await context.Cars.ToListAsync(cancellationToken);
            var clients = await context.Clients.ToListAsync(cancellationToken);

            var quote1 = new Quote(DefaultDealerId, cars[3], clients[0], 21500, DateTime.UtcNow.AddDays(15), "Cotización aceptada", DateTime.UtcNow.AddDays(-2));
            quote1.Accept(DateTime.UtcNow.AddDays(-1));

            var quote2 = new Quote(DefaultDealerId, cars[4], clients[1], 63000, DateTime.UtcNow.AddDays(7), "Cotización pendiente", DateTime.UtcNow.AddDays(-1));

            var quote3 = new Quote(DefaultDealerId, cars[0], clients[2], 24000, DateTime.UtcNow.AddDays(10), "Cotización rechazada", DateTime.UtcNow.AddDays(-5));
            quote3.Reject("El precio es muy elevado", DateTime.UtcNow.AddDays(-4));

            context.Quotes.AddRange(new[] { quote1, quote2, quote3 });
            await context.SaveChangesAsync(cancellationToken);
        }

        // 5. Transacciones Financieras
        if (!await context.Transactions.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            var categories = await context.TransactionCategories.ToListAsync(cancellationToken);
            var cars = await context.Cars.ToListAsync(cancellationToken);
            var clients = await context.Clients.ToListAsync(cancellationToken);
            var sales = await context.Sales.ToListAsync(cancellationToken);

            var catVenta = categories.First(c => c.Name == "Venta de Auto");
            var catServicio = categories.First(c => c.Name == "Servicio Técnico");
            var catPublicidad = categories.First(c => c.Name == "Publicidad");
            var catMantenimiento = categories.First(c => c.Name == "Mantenimiento");

            var transactions = new List<FinancialTransaction>
            {
                new FinancialTransaction(DefaultDealerId, TransactionType.Income, 24500, "Venta de Corolla", Domain.Financial.Attributes.PaymentMethod.Cash, catVenta, cars[0], clients[0], sales[0], DateTime.UtcNow.AddDays(-5)),
                new FinancialTransaction(DefaultDealerId, TransactionType.Income, 5000, "Servicio de mantenimiento Corolla", Domain.Financial.Attributes.PaymentMethod.BankTransfer, catServicio, cars[3], clients[0], null, DateTime.UtcNow.AddDays(-1)),
                new FinancialTransaction(DefaultDealerId, TransactionType.Expense, 1500, "Campaña Facebook Ads", Domain.Financial.Attributes.PaymentMethod.CreditCard, catPublicidad, null, null, null, DateTime.UtcNow.AddDays(-3)),
                new FinancialTransaction(DefaultDealerId, TransactionType.Expense, 800, "Limpieza y acondicionado Hilux", Domain.Financial.Attributes.PaymentMethod.Cash, catMantenimiento, cars[1], null, null, DateTime.UtcNow.AddDays(-2)),
                new FinancialTransaction(DefaultDealerId, TransactionType.Income, 44000, "Venta de Hilux", Domain.Financial.Attributes.PaymentMethod.BankTransfer, catVenta, cars[1], clients[1], sales[1], DateTime.UtcNow.AddDays(-2))
            };

            context.Transactions.AddRange(transactions);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
