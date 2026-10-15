using Application.Abstractions;
using Application.Abstractions.Tenancy;
using Application.Quotes.CreateInquiry;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Leads;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Quotes;

/// <summary>
/// sfc C0.1 / RN-045 / ADR C23: a public inquiry carries the visitor's first-touch attribution. The lead stores it, its
/// source comes from it (utm_source, else gclid/fbclid, else Web), and a returning lead keeps its original first touch.
/// </summary>
public class CreateInquiryAttributionTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime FirstTouch = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CreateInquiryCommandHandler Handler(TestApplicationDbContext context)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(false);
        var allocator = new Mock<IRoundRobinLeadAllocator>();
        return new CreateInquiryCommandHandler(context, new FakeDateTimeProvider(), tenant.Object, allocator.Object);
    }

    private static async Task<Car> SeedCarAsync(TestApplicationDbContext context)
    {
        var marca = new Marca("Peugeot");
        var modelo = new Modelo("208", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 1600, 1000, 2021, "ATR001", "desc", 15000m, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        await context.SaveChangesAsync();
        return car;
    }

    private static InquiryAttribution Touch(string? source = null, string? gclid = null, string? campaign = null) =>
        new(source, "cpc", campaign, null, null, "https://google.com/", "/catalogo", gclid, null, FirstTouch);

    private static CreateInquiryCommand Inquiry(Guid carId, InquiryAttribution? attribution, string email = "ana@buyer.com") =>
        new(carId, "Ana", "Fernandez", email, "1122334455", "Me interesa", attribution);

    [Fact]
    public async Task Handle_Should_StoreTheFirstTouch_AndDeriveTheSourceFromIt()
    {
        using var context = CreateContext();
        var car = await SeedCarAsync(context);

        var result = await Handler(context).Handle(Inquiry(car.Id, Touch("Facebook", campaign: "otono")), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var lead = await context.Leads.SingleAsync();
        lead.Source.Should().Be(LeadSource.Facebook);
        lead.Attribution!.UtmCampaign.Should().Be("otono");
        lead.Attribution.LandingPath.Should().Be("/catalogo");
        lead.Attribution.FirstTouchAtUtc.Should().Be(FirstTouch);
    }

    [Fact]
    public async Task Handle_Should_UseTheClickId_WhenThereIsNoUtmSource()
    {
        using var context = CreateContext();
        var car = await SeedCarAsync(context);

        await Handler(context).Handle(Inquiry(car.Id, Touch(gclid: "G1")), CancellationToken.None);

        (await context.Leads.SingleAsync()).Source.Should().Be(LeadSource.Google);
    }

    [Fact]
    public async Task Handle_Should_StayWeb_WithoutAttribution()
    {
        using var context = CreateContext();
        var car = await SeedCarAsync(context);

        await Handler(context).Handle(Inquiry(car.Id, attribution: null), CancellationToken.None);

        var lead = await context.Leads.SingleAsync();
        lead.Source.Should().Be(LeadSource.Web);
        lead.Attribution.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_KeepTheOriginalFirstTouch_OfAReturningLead()
    {
        using var context = CreateContext();
        var car = await SeedCarAsync(context);
        await Handler(context).Handle(Inquiry(car.Id, Touch("google", campaign: "primera")), CancellationToken.None);

        await Handler(context).Handle(Inquiry(car.Id, Touch("facebook", campaign: "segunda")), CancellationToken.None);

        var lead = await context.Leads.SingleAsync();
        lead.Source.Should().Be(LeadSource.Google);
        lead.Attribution!.UtmCampaign.Should().Be("primera");
    }
}
