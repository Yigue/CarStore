using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.Leads.RegisterWhatsAppClickIntent;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Leads;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Application.UnitTests.Leads.WhatsAppClickIntent;

/// <summary>sfc C0.3 / B14: a WhatsApp click-to-chat is registered as a minimal, visible Lead/LeadActivity.</summary>
public class RegisterWhatsAppClickIntentCommandHandlerTests
{
    private static readonly Guid DealerA = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static ICurrentTenantService TenantService(Guid? dealerId)
    {
        var mock = new Mock<ICurrentTenantService>();
        mock.SetupGet(t => t.HasTenant).Returns(dealerId.HasValue);
        mock.SetupGet(t => t.DealerId).Returns(dealerId ?? Guid.Empty);
        return mock.Object;
    }

    private static RegisterWhatsAppClickIntentCommandHandler CreateHandler(
        TestApplicationDbContext context, Guid? tenantDealerId = null) =>
        new(context, new FakeDateTimeProvider(), TenantService(tenantDealerId));

    private static async Task<Car> SeedCarAsync(TestApplicationDbContext context, Guid dealerId, string patente)
    {
        var marca = new Marca($"Peugeot-{patente}");
        var modelo = new Modelo($"208-{patente}", marca.Id);
        var car = new Car(
            dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2021, patente, "desc", 15000m, DateTime.UtcNow);

        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        await context.SaveChangesAsync();
        return car;
    }

    [Fact]
    public async Task Handle_Should_CreateAVisibleLead_WithCarReference()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, DealerA, "WAB001");

        var result = await CreateHandler(context).Handle(
            new RegisterWhatsAppClickIntentCommand(car.Id, "ABCD1234"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        Lead lead = await context.Leads.SingleAsync();
        lead.DealerId.Should().Be(DealerA);
        lead.Source.Should().Be(LeadSource.WhatsApp);
        lead.InterestedVehicleId.Should().Be(car.Id);
        lead.Status.Should().Be(LeadStatus.Nuevo);
        result.Value.Should().Be(lead.Id);

        LeadActivity activity = await context.LeadActivities.SingleAsync();
        activity.LeadId.Should().Be(lead.Id);
        activity.Type.Should().Be(LeadActivityType.NoteAdded);
        activity.Description.Should().Contain("#LABCD1234");
    }

    [Fact]
    public async Task Handle_Should_CreateANewLead_ForEachClick_WhenThereIsNoVisitorIdentity()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, DealerA, "WAB002");
        var handler = CreateHandler(context);

        await handler.Handle(new RegisterWhatsAppClickIntentCommand(car.Id, "CODE0001"), CancellationToken.None);
        await handler.Handle(new RegisterWhatsAppClickIntentCommand(car.Id, "CODE0001"), CancellationToken.None);

        // No visitor identity exists at click time (no email/phone); each click is its own lead,
        // documented as a deliberate judgment call in the handler.
        (await context.Leads.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Handle_Should_ResolveDealer_ViaTenant_WhenNoCarIdIsGiven()
    {
        using var context = CreateContext();

        var result = await CreateHandler(context, tenantDealerId: DealerA).Handle(
            new RegisterWhatsAppClickIntentCommand(null, "GENERIC1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.Leads.SingleAsync()).DealerId.Should().Be(DealerA);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenNoCarId_AndNoTenantResolved()
    {
        using var context = CreateContext();

        var result = await CreateHandler(context, tenantDealerId: null).Handle(
            new RegisterWhatsAppClickIntentCommand(null, "GENERIC1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        context.Leads.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenCarIdDoesNotExist()
    {
        using var context = CreateContext();

        var result = await CreateHandler(context).Handle(
            new RegisterWhatsAppClickIntentCommand(Guid.NewGuid(), "GENERIC1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
