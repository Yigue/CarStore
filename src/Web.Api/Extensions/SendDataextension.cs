#pragma warning disable IDE0008 // Convertir en namespace con ámbito de archivo
#pragma warning disable IDE0007

using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Domain.Cars;
using Domain.Clients;
using Domain.Quotes;
using Domain.Sales;
using Bogus;
using System.Security.Cryptography;
using Domain.Cars.Attributes;
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

        try
        {
            context.Database.Migrate();
        }
        catch
        {
            // Ignore if database doesn't support migrations or is already created
        }

        SeedCars(context);
        SeedClients(context);
        SeedQuotes(context);
        SeedSales(context);
        SeedTransactions(context);
    }

    // URLs públicas de Unsplash (licencia libre) para evitar dependencia de blob storage en dev.
    private static readonly string[] CarImageUrls =
    [
        "https://images.unsplash.com/photo-1494976388531-d1058494cdd8?w=1200&q=80", // muscle car
        "https://images.unsplash.com/photo-1542362567-b07e54358753?w=1200&q=80", // sedan blanco
        "https://images.unsplash.com/photo-1503376780353-7e6692767b70?w=1200&q=80", // deportivo rojo
        "https://images.unsplash.com/photo-1605559424843-9e4c228bf1c2?w=1200&q=80", // suv negro
        "https://images.unsplash.com/photo-1552519507-da3b142c6e3d?w=1200&q=80", // deportivo amarillo
        "https://images.unsplash.com/photo-1583121274602-3e2820c69888?w=1200&q=80", // sedan azul
        "https://images.unsplash.com/photo-1568844293986-8d0400bd4745?w=1200&q=80", // suv plata
        "https://images.unsplash.com/photo-1494976388531-d1058494cdd8?w=1200&q=80", // backup
    ];

    private static void SeedCars(ApplicationDbContext context)
    {
        if (!context.Set<Car>().IgnoreQueryFilters().Any())
        {
            // BrandsSeeder ya creó marcas y modelos. Las reusamos para no duplicar.
            // Si por algún motivo no hubiera, sembramos un set mínimo aquí.
            var marcas = context.Set<Marca>().IgnoreQueryFilters().ToList();
            if (marcas.Count == 0)
            {
                marcas =
                [
                    new Marca("Toyota"),
                    new Marca("Ford"),
                    new Marca("Chevrolet"),
                    new Marca("Honda"),
                    new Marca("Volkswagen")
                ];
                context.AddRange(marcas);
                context.SaveChanges();
            }

            var modelos = context.Set<Modelo>().IgnoreQueryFilters().ToList();
            if (modelos.Count == 0)
            {
                foreach (var marca in marcas)
                {
                    var modeloFaker = new Faker<Modelo>()
                        .CustomInstantiator(f => new Modelo(f.Vehicle.Model(), marca.Id));
                    modelos.AddRange(modeloFaker.Generate(2));
                }
                context.AddRange(modelos);
                context.SaveChanges();
            }

            if (marcas.Count == 0 || modelos.Count == 0)
            {
                throw new InvalidOperationException("Marcas or Modelos list is empty.");
            }

        var carFaker = new Faker<Car>()
            .CustomInstantiator(f => new Car(
                Guid.Parse("11111111-1111-1111-1111-111111111111"), // Usar ID fijo de desarrollo
                f.PickRandom(marcas),
                f.PickRandom(modelos),
                f.PickRandom<Color>(),
                TypeCar.Sedan,
                StatusCar.New,
                StatusServiceCar.EnVenta,
                f.Random.Int(2, 5),
                f.Random.Int(4, 7),
                f.Random.Int(1000, 3000),
                f.Random.Int(0, 100000),
                f.Random.Int(2015, 2024),
                f.Random.Replace("??###??"),
                f.Lorem.Sentence(),
                f.Random.Decimal(15000, 50000),
                DateTime.Now.ToUniversalTime()
            ));

            var cars = carFaker.Generate(8);
            context.AddRange(cars);
            context.SaveChanges();

            // Sembrar 2-3 imágenes por auto. La primera es la imagen principal.
            var imageFaker = new Faker();
            foreach (var car in cars)
            {
                var count = imageFaker.Random.Int(2, 3);
                var pickedUrls = imageFaker.PickRandom(CarImageUrls, count).ToList();
                for (var i = 0; i < pickedUrls.Count; i++)
                {
                    var image = new CarImage(
                        carId: car.Id,
                        imageUrl: pickedUrls[i],
                        isCover: i == 0,
                        displayOrder: i);
                    context.Add(image);
                }
            }
            context.SaveChanges();
        }
    }

    private static void SeedClients(ApplicationDbContext context)
    {
        if (!context.Set<Client>().IgnoreQueryFilters().Any())
        {
            var clientFaker = new Faker<Client>()
                .CustomInstantiator(f => new Client(
                    Guid.Parse("11111111-1111-1111-1111-111111111111"), // DealerId fijo
                    f.Name.FirstName(),
                    f.Name.LastName(),
                    f.Random.Replace("#######"),
                    f.Internet.Email(),
                    f.Phone.PhoneNumber("##########"),
                    f.Address.FullAddress(),
                    f.Date.Past(2).ToUniversalTime()
                ));

            var clients = clientFaker.Generate(5);
            context.AddRange(clients);
            context.SaveChanges();
        }
    }

    private static void SeedQuotes(ApplicationDbContext context)
    {
        if (!context.Set<Quote>().IgnoreQueryFilters().Any())
        {
            var cars = context.Set<Car>().IgnoreQueryFilters().ToList();
            var clients = context.Set<Client>().IgnoreQueryFilters().ToList();
            
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
            Guid.Parse("11111111-1111-1111-1111-111111111111"), // DealerId fijo de desarrollo
            car,
            client,
            null, // Lead
            f.Random.Decimal((car.Price * 0.9m).Amount, (car.Price * 1.1m).Amount),
            f.PickRandom<Domain.Quotes.Attributes.PaymentMethod>(),
            f.Date.Future().ToUniversalTime(), // Ensure UTC
            f.Lorem.Sentence(),
            f.Date.Recent().ToUniversalTime()
        );
    }

    private static void SeedSales(ApplicationDbContext context)
    {
        if (!context.Set<Sale>().IgnoreQueryFilters().Any())
        {
            var cars = context.Set<Car>().IgnoreQueryFilters().ToList();
            var clients = context.Set<Client>().IgnoreQueryFilters().ToList();

            // Validar que hay datos
            if (cars.Count == 0 || clients.Count == 0)
            {
                throw new InvalidOperationException(
                    "No se pueden generar ventas: La lista de Cars o Clients está vacía."
                );
            }

            var saleFaker = new Faker<Sale>()
                .CustomInstantiator(f => new Sale(
                    Guid.Parse("11111111-1111-1111-1111-111111111111"), // DealerId fijo de desarrollo
                    f.PickRandom(cars).Id, // Usar PickRandom de Bogus
                    f.PickRandom(clients).Id,
                    f.PickRandom(cars).Price.Amount, // Obtener precio del Car seleccionado
                    PaymentMethod.Cash,
                    f.Random.Replace("#######"),
                    f.Lorem.Sentence(),
                    f.Date.Recent().ToUniversalTime()
                ));

            var sales = saleFaker.Generate(5);
            context.AddRange(sales);
            context.SaveChanges();
        }
    }

 private static void SeedTransactions(ApplicationDbContext context)
{
    if (!context.Set<TransactionCategory>().IgnoreQueryFilters().Any())
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

    if (!context.Set<FinancialTransaction>().IgnoreQueryFilters().Any())
    {
        var categories = context.Set<TransactionCategory>().IgnoreQueryFilters().ToList();
        var sales = context.Set<Sale>().IgnoreQueryFilters().ToList();
        var car = context.Set<Car>().IgnoreQueryFilters().ToList();
        var client = context.Set<Client>().IgnoreQueryFilters().ToList();

        if (categories.Count == 0 || sales.Count == 0)
        {
            throw new InvalidOperationException("Transaction categories or sales list is empty.");
        }

        var transactionFaker = new Faker<FinancialTransaction>()
            .CustomInstantiator(f => new FinancialTransaction(
                Guid.Parse("11111111-1111-1111-1111-111111111111"), // DealerId fijo de desarrollo
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
