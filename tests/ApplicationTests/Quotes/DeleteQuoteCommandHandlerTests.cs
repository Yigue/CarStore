using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Quotes.Delete;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Quotes;

public class DeleteQuoteCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static async Task<Quote> SeedQuoteAsync(TestApplicationDbContext context)
    {
        var dealerId = Guid.NewGuid();
        var marca = new Marca("Peugeot");
        var modelo = new Modelo("208", marca.Id);
        var car = new Car(dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2021, "DEL001", "desc", 15000m, DateTime.UtcNow);
        var client = new Client(dealerId, "Eve", "Black", "999", "eve@test.com", "444", "Addr", DateTime.UtcNow);
        var quote = new Quote(dealerId, car, client, null, 14000m, PaymentMethod.Contado, DateTime.UtcNow.AddDays(7), "c", DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.Add(client);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();
        return quote;
    }

    [Fact]
    public async Task Handle_Should_SoftDelete_SettingIsDeleted_WithoutRemovingRow()
    {
        using var context = CreateContext();
        var quote = await SeedQuoteAsync(context);
        var dateProvider = new FakeDateTimeProvider();
        var handler = new DeleteQuoteCommandHandler(context, dateProvider);

        var result = await handler.Handle(new DeleteQuoteCommand(quote.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // Row must still exist (soft delete, not physical removal).
        var persisted = await context.Quotes.IgnoreQueryFilters().FirstAsync(q => q.Id == quote.Id);
        persisted.IsDeleted.Should().BeTrue();
        persisted.DeletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenQuoteDoesNotExist()
    {
        using var context = CreateContext();
        var dateProvider = new FakeDateTimeProvider();
        var handler = new DeleteQuoteCommandHandler(context, dateProvider);
        var missingId = Guid.NewGuid();

        var result = await handler.Handle(new DeleteQuoteCommand(missingId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(QuoteErrors.NotFound(missingId));
    }
}
