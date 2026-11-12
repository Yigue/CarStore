using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.Quotes.Reject;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace Application.UnitTests.Quotes;

public class RejectQuoteCommandHandlerTests
{
    private static readonly Guid DealerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static ICurrentTenantService CreateTenantService(Guid? dealerId = null)
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.SetupGet(t => t.HasTenant).Returns(true);
        tenantMock.SetupGet(t => t.DealerId).Returns(dealerId ?? DealerId);
        return tenantMock.Object;
    }

    private static RejectQuoteCommandHandler CreateHandler(TestApplicationDbContext context, Guid? dealerId = null) =>
        new RejectQuoteCommandHandler(context, new FakeDateTimeProvider(), CreateTenantService(dealerId));

    private static async Task<Quote> SeedQuoteAsync(
        TestApplicationDbContext context,
        Action<Quote>? mutateQuote = null)
    {
        var dealerId = DealerId;
        var marca = new Marca("Peugeot");
        var modelo = new Modelo("208", marca.Id);
        var car = new Car(dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2021, "REJ001", "desc", 15000m, DateTime.UtcNow);
        var client = new Client(dealerId, "Eve", "Black", "999", "eve@test.com", "444", "Addr", DateTime.UtcNow);
        var quote = new Quote(dealerId, car, client, null, 14000m, PaymentMethod.Contado, DateTime.UtcNow.AddDays(7), "c", DateTime.UtcNow);
        mutateQuote?.Invoke(quote);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.Add(client);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();
        return quote;
    }

    [Fact]
    public async Task Handle_Should_ReturnAlreadyProcessedConflict_WhenQuoteIsAlreadyAccepted()
    {
        using var context = CreateContext();
        var quote = await SeedQuoteAsync(context, q => q.Accept(DateTime.UtcNow));
        var handler = CreateHandler(context);

        var command = new RejectQuoteCommand(quote.Id, "Already decided");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Quotes.AlreadyProcessed");
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Handle_Should_ReturnAlreadyProcessedConflict_WhenQuoteIsAlreadyRejected()
    {
        using var context = CreateContext();
        var quote = await SeedQuoteAsync(context, q => q.Reject("First rejection", DateTime.UtcNow));
        var handler = CreateHandler(context);

        var command = new RejectQuoteCommand(quote.Id, "Second rejection attempt");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Quotes.AlreadyProcessed");
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }
}
