using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Quotes.Get;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Leads;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Quotes;

public class GetQuotesQueryHandlerTests
{
    private static readonly Guid DealerId = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static async Task<Car> SeedCarAsync(TestApplicationDbContext context)
    {
        var marca = new Marca("Peugeot");
        var modelo = new Modelo("208", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Reservado, 4, 5, 1600, 1000, 2021, "DEL001", "desc", 15000m, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        await context.SaveChangesAsync();
        return car;
    }

    [Fact]
    public async Task Handle_Should_ProjectOriginLeadId_ForClientLinkedQuote()
    {
        using var context = CreateContext();
        var car = await SeedCarAsync(context);
        var originLead = Lead.Create(DealerId, "Origin Lead", "origin@test.com", "555", LeadSource.Web, DateTime.UtcNow);
        context.Leads.Add(originLead);
        var client = new Client(DealerId, "Eve", "Black", "999", "eve@test.com", "444", "Addr", DateTime.UtcNow, originLeadId: originLead.Id);
        context.Clients.Add(client);
        var quote = new Quote(DealerId, car, client, null, 14000m, PaymentMethod.Contado, DateTime.UtcNow.AddDays(7), "c", DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var handler = new GetQuotesQueryHandler(context);
        var result = await handler.Handle(new GetQuotesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var response = result.Value.Single(q => q.Id == quote.Id);
        response.OriginLeadId.Should().Be(originLead.Id);
        response.ConvertedClientId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_ProjectConvertedClientId_ForLeadLinkedQuote()
    {
        using var context = CreateContext();
        var car = await SeedCarAsync(context);
        var lead = Lead.Create(DealerId, "John Doe", "john@test.com", "555", LeadSource.Web, DateTime.UtcNow);
        var convertedClientId = Guid.NewGuid();
        lead.MarkConverted(convertedClientId);
        context.Leads.Add(lead);
        var quote = new Quote(DealerId, car, null, lead, 14000m, PaymentMethod.Contado, DateTime.UtcNow.AddDays(7), "c", DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var handler = new GetQuotesQueryHandler(context);
        var result = await handler.Handle(new GetQuotesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var response = result.Value.Single(q => q.Id == quote.Id);
        response.ConvertedClientId.Should().Be(convertedClientId);
        response.OriginLeadId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_NullBothXrefs_WhenNoCorrelation()
    {
        using var context = CreateContext();
        var car = await SeedCarAsync(context);
        var client = new Client(DealerId, "Eve", "Black", "999", "eve@test.com", "444", "Addr", DateTime.UtcNow);
        context.Clients.Add(client);
        var quote = new Quote(DealerId, car, client, null, 14000m, PaymentMethod.Contado, DateTime.UtcNow.AddDays(7), "c", DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var handler = new GetQuotesQueryHandler(context);
        var result = await handler.Handle(new GetQuotesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var response = result.Value.Single(q => q.Id == quote.Id);
        response.OriginLeadId.Should().BeNull();
        response.ConvertedClientId.Should().BeNull();
    }
}
