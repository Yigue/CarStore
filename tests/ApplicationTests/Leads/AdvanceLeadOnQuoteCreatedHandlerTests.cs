using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Leads.UpdateStatus;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Leads;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Domain.Quotes.Events;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Leads;

/// <summary>
/// Negociación was the one artifact stage a user had to set by hand: a booked demo advanced the
/// lead and a registered sale advanced it, but a quote only spoke to the pipeline when accepted,
/// jumping straight to Ganado. That gap is why cancelling the quote dialog left a lead negotiating
/// with no number on the table.
/// </summary>
public class AdvanceLeadOnQuoteCreatedHandlerTests
{
    private static readonly Guid DealerId = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static async Task<Car> SeedCarAsync(TestApplicationDbContext context, string patente)
    {
        var marca = new Marca($"Fiat-{patente}");
        var modelo = new Modelo($"Cronos-{patente}", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.Gray, TypeCar.Sedan, StatusCar.Used,
            StatusServiceCar.Disponible, 4, 5, 1300, 30000, 2021, patente, "desc", 9000m, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        await context.SaveChangesAsync();
        return car;
    }

    private static Lead NewLead() =>
        Lead.Create(DealerId, "Ana Fernandez", "ana@test.com", "1", LeadSource.Web, DateTime.UtcNow);

    private static async Task<Quote> SeedQuoteAsync(
        TestApplicationDbContext context, Car car, Lead lead)
    {
        var quote = new Quote(DealerId, car, null, lead, 9000m, PaymentMethod.Contado,
            DateTime.UtcNow.AddDays(30), "", DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();
        return quote;
    }

    [Fact]
    public async Task Handle_Should_AdvanceTheLeadToNegociacion()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, "NEG001");
        Lead lead = NewLead();
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        Quote quote = await SeedQuoteAsync(context, car, lead);

        await new AdvanceLeadOnQuoteCreatedHandler(context).Handle(
            new QuoteCreatedDomainEvent(quote.Id, car.Id, Guid.Empty, quote.ProposedPrice),
            CancellationToken.None);

        (await context.Leads.SingleAsync()).Status.Should().Be(LeadStatus.Negociacion);
    }

    /// <summary>
    /// A second offer, or one attached after the deal closed, must not drag the lead backwards.
    /// </summary>
    [Theory]
    [InlineData(LeadStatus.Negociacion)]
    [InlineData(LeadStatus.Ganado)]
    public async Task Handle_Should_NotPullTheLeadBack(LeadStatus current)
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, $"BAK{(int)current:D3}");
        Lead lead = NewLead();
        lead.ForceStatus(current);
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        Quote quote = await SeedQuoteAsync(context, car, lead);

        await new AdvanceLeadOnQuoteCreatedHandler(context).Handle(
            new QuoteCreatedDomainEvent(quote.Id, car.Id, Guid.Empty, quote.ProposedPrice),
            CancellationToken.None);

        (await context.Leads.SingleAsync()).Status.Should().Be(current);
    }

    [Fact]
    public async Task Handle_Should_Ignore_AQuoteWithNoLead()
    {
        using var context = CreateContext();

        Func<Task> act = () => new AdvanceLeadOnQuoteCreatedHandler(context).Handle(
            new QuoteCreatedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty,
                new Domain.Shared.ValueObjects.Money(1m)),
            CancellationToken.None);

        await act.Should().NotThrowAsync("an outbox retry must not wedge on a missing record");
    }
}
