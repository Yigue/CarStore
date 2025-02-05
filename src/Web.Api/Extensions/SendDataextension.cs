#pragma warning disable IDE0008 // Convertir en namespace con ámbito de archivo
#pragma warning disable IDE0007

using Infrastructure.Database;
using Domain.Cars;
using Domain.Clients;
using Domain.Quotes;
using Domain.Sales;
using Bogus;
using System.Security.Cryptography;
using Domain.Cars.Atribbutes;
using Domain.Financial.Attributes;
using System.Transactions;
using Domain.Financial;

namespace Web.Api.Extensions;

public static class SeedDataExtensions
{
    public static void SeedTestData(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var service = scope.ServiceProvider;
        var context = service.GetRequiredService<ApplicationDbContext>();

        SeedCars(context);
        SeedClients(context);
        SeedQuotes(context);
        SeedSales(context);
        SeedTransactions(context);
    }

    private static void SeedCars(ApplicationDbContext context)
    {
        if (!context.Set<Car>().Any())
        {
             var marcas = new List<Marca>{
            new Marca("Toyota"),
            new Marca("Ford"),
            new Marca("Chevrolet"),
            new Marca("Honda"),
            new Marca("Volkswagen")
        };
        context.AddRange(marcas);
        context.SaveChanges(); // Guardar Marcas para obtener sus IDs

        // 2. Crear Modelos asociados a las Marcas
        var modelos = new List<Modelo>();
        foreach (var marca in marcas)
        {
            var modeloFaker = new Faker<Modelo>()
                .CustomInstantiator(f => new Modelo(
                    f.Vehicle.Model(),
                    marca.Id // Usar el ID de la marca ya guardada
                ));
            modelos.AddRange(modeloFaker.Generate(2)); // 2 modelos por marca
        }
        context.AddRange(modelos);
        context.SaveChanges(); // Guardar Modelos para obtener sus IDs

        if (marcas.Count == 0 || modelos.Count == 0)
        {
            throw new InvalidOperationException("Marcas or Modelos list is empty.");
        }

        var carFaker = new Faker<Car>()
            .CustomInstantiator(f => new Car(
                f.PickRandom(marcas),
                f.PickRandom(modelos),
                f.PickRandom<Color>(),
                TypeCar.Sedan,
                StatusCar.New,
                statusServiceCar.EnVenta,
                f.Random.Int(2, 5),
                f.Random.Int(4, 7),
                f.Random.Int(1000, 3000),
                f.Random.Int(0, 100000),
                f.Random.Int(2015, 2024),
                f.Random.Replace("???-####"),
                f.Lorem.Sentence(),
                f.Random.Decimal(15000, 50000),
                DateTime.Now.ToUniversalTime()
            ));

            var cars = carFaker.Generate(5) ;
            context.AddRange(cars);
            context.SaveChanges();
        }
    }

    private static void SeedClients(ApplicationDbContext context)
    {
        if (!context.Set<Client>().Any())
        {
            var clientFaker = new Faker<Client>()
                .CustomInstantiator(f => new Client(
                    f.Name.FirstName(),
                    f.Name.LastName(),
                    f.Random.Replace("#######"),
                    f.Internet.Email(),
                    f.Phone.PhoneNumber(),
                    f.Address.FullAddress()
                ));

            var clients = clientFaker.Generate(5);
            context.AddRange(clients);
            context.SaveChanges();
        }
    }

    private static void SeedQuotes(ApplicationDbContext context)
    {
        if (!context.Set<Quote>().Any())
        {
            var cars = context.Set<Car>().ToList();
            var clients = context.Set<Client>().ToList();
            
            if (cars.Count == 0 || clients.Count == 0)
            {
                // Handle the empty list case, e.g., log a message or skip seeding
                return;
            }

            using var rng = RandomNumberGenerator.Create();
            var quoteFaker = new Faker<Quote>()
                .CustomInstantiator(f => CreateQuote(f, cars, clients, rng));

            var quotes = quoteFaker.Generate(5);
            context.AddRange(quotes);
            context.SaveChanges();
        }
    }

    private static Quote CreateQuote(Faker f, List<Car> cars, List<Client> clients, RandomNumberGenerator rng)
    {
        if (cars.Count == 0 || clients.Count == 0)
        {
            throw new InvalidOperationException("Cars or clients list is empty.");
        }

        var car = cars[RandomNumber(rng, cars.Count)];
        var client = clients[RandomNumber(rng, clients.Count)];
        return new Quote(
            car,
            client,
            f.Random.Decimal(car.Price * 0.9m, car.Price * 1.1m),
            f.Date.Future().ToUniversalTime(), // Ensure UTC
            f.Lorem.Sentence()
        );
    }

   private static void SeedSales(ApplicationDbContext context)
    {
        if (!context.Set<Sale>().Any())
        {
            var cars = context.Set<Car>().ToList();
            var clients = context.Set<Client>().ToList();

            // Validar que hay datos
            if (cars.Count == 0 || clients.Count == 0)
            {
                throw new InvalidOperationException(
                    "No se pueden generar ventas: La lista de Cars o Clients está vacía."
                );
            }

            var saleFaker = new Faker<Sale>()
                .CustomInstantiator(f => new Sale(
                    f.PickRandom(cars).Id, // Usar PickRandom de Bogus
                    f.PickRandom(clients).Id,
                    f.PickRandom(cars).Price, // Obtener precio del Car seleccionado
                    PaymentMethod.Cash,
                    f.Random.Replace("#######"),
                    f.Lorem.Sentence()
                ));

            var sales = saleFaker.Generate(5);
            context.AddRange(sales);
            context.SaveChanges();
        }
    }

 private static void SeedTransactions(ApplicationDbContext context)
{
    if (!context.Set<TransactionCategory>().Any())
    {
        var categories = new List<TransactionCategory>
        {
            new TransactionCategory("Food", "Expenses for food and dining", TransactionType.Expense),
            new TransactionCategory("Utilities", "Monthly utility bills", TransactionType.Expense),
            new TransactionCategory("Entertainment", "Leisure and entertainment expenses", TransactionType.Expense),
            new TransactionCategory("Rent", "Monthly rent payments", TransactionType.Expense),
            new TransactionCategory("Miscellaneous", "Other miscellaneous expenses", TransactionType.Expense)
        };
        context.AddRange(categories);
        context.SaveChanges();
    }

    if (!context.Set<FinancialTransaction>().Any())
    {
        var categories = context.Set<TransactionCategory>().ToList();
        var sales = context.Set<Sale>().ToList();
        var car = context.Set<Car>().ToList();
        var client = context.Set<Client>().ToList();

        if (categories.Count == 0 || sales.Count == 0)
        {
            throw new InvalidOperationException("Transaction categories or sales list is empty.");
        }

        var transactionFaker = new Faker<FinancialTransaction>()
            .CustomInstantiator(f => new FinancialTransaction(
                f.PickRandom<TransactionType>(),
                f.Random.Decimal(10, 1000),
                f.Lorem.Sentence(),
                f.PickRandom<PaymentMethod>(),
                f.PickRandom(categories),
                f.PickRandom(car), // Ensure sale reference is valid
                f.PickRandom(client), // Ensure sale reference is valid
                f.PickRandom(sales) // Ensure sale reference is valid
            ));

        var transactions = transactionFaker.Generate(10);
        context.AddRange(transactions);
        context.SaveChanges();
    }
}

   private static int RandomNumber(RandomNumberGenerator rng, int maxValue)
    {
        if (maxValue == 0)
        {
            return 0; // or handle this case as needed
        }
        byte[] randomBytes = new byte[4];
        rng.GetBytes(randomBytes);
        return Math.Abs(BitConverter.ToInt32(randomBytes, 0)) % maxValue;
}
}
