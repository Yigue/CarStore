using Application.DealerSettings.Commands.UpdateDealerLanding;
using Application.Abstractions.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.UnitTests.DealerSettings;

/// <summary>
/// CFG-06: a dealership rewrites its own landing page instead of every dealer showing the same
/// hardcoded copy.
/// </summary>
public sealed class UpdateDealerLandingCommandHandlerTests
{
    private static readonly Guid TestDealerId = Guid.Parse("aaaa0000-0000-0000-0000-000000000001");

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options, TestDealerId);
    }

    private static Mock<ICurrentTenantService> MockTenant(Guid dealerId)
    {
        var mock = new Mock<ICurrentTenantService>();
        mock.Setup(t => t.DealerId).Returns(dealerId);
        mock.Setup(t => t.HasTenant).Returns(true);
        return mock;
    }

    private static DealerSettingsEntity SeedSettings(TestApplicationDbContext context)
    {
        var settings = new DealerSettingsEntity(
            dealerId: TestDealerId,
            dealerName: "Test Dealer",
            contactEmail: "test@test.com");
        context.DealerSettings.Add(settings);
        context.SaveChanges();
        return settings;
    }

    [Fact]
    public async Task Handle_UpdatesTheLandingText_AndPersistsIt()
    {
        using var context = CreateContext();
        SeedSettings(context);
        var handler = new UpdateDealerLandingCommandHandler(context, MockTenant(TestDealerId).Object);

        var result = await handler.Handle(
            new UpdateDealerLandingCommand(
                HistoryText: "Fundada en 1990.",
                MissionText: "Misión de prueba.",
                VisionText: "Visión de prueba.",
                ValuesText: "Valores de prueba.",
                CarouselSlides: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var persisted = await context.DealerSettings.SingleAsync(s => s.DealerId == TestDealerId);
        persisted.HistoryText.Should().Be("Fundada en 1990.");
        persisted.MissionText.Should().Be("Misión de prueba.");
    }

    [Fact]
    public async Task Handle_PersistsTheCarousel()
    {
        using var context = CreateContext();
        SeedSettings(context);
        var handler = new UpdateDealerLandingCommandHandler(context, MockTenant(TestDealerId).Object);

        var result = await handler.Handle(
            new UpdateDealerLandingCommand(
                HistoryText: null,
                MissionText: null,
                VisionText: null,
                ValuesText: null,
                CarouselSlides: [new CarouselSlideDto("https://cdn/1.jpg", "Bienvenidos", "Sub", "Desc", "Ver más", "/catalogo")]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var persisted = await context.DealerSettings.SingleAsync(s => s.DealerId == TestDealerId);
        var slides = persisted.GetCarouselSlides();
        slides.Should().ContainSingle();
        slides[0].ImageUrl.Should().Be("https://cdn/1.jpg");
        slides[0].Title.Should().Be("Bienvenidos");
    }

    [Fact]
    public async Task Handle_Fails_WhenDealerSettingsDoNotExist()
    {
        using var context = CreateContext();
        var handler = new UpdateDealerLandingCommandHandler(context, MockTenant(TestDealerId).Object);

        var result = await handler.Handle(
            new UpdateDealerLandingCommand(null, null, null, null, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DealerSettings.NotFound");
    }

    [Fact]
    public async Task Handle_Fails_WhenASlideHasNoImage_AndWritesNothing()
    {
        using var context = CreateContext();
        SeedSettings(context);
        var handler = new UpdateDealerLandingCommandHandler(context, MockTenant(TestDealerId).Object);

        var result = await handler.Handle(
            new UpdateDealerLandingCommand(
                "Historia que no debe guardarse",
                null,
                null,
                null,
                [new CarouselSlideDto("", "Sin imagen", null, null, null, null)]),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DealerSettings.InvalidLanding");

        var persisted = await context.DealerSettings.SingleAsync(s => s.DealerId == TestDealerId);
        persisted.HistoryText.Should().BeNull("a rejected slide must not let the rest of the update through");
    }

    [Fact]
    public async Task Handle_OnlyTouchesTheCallingDealersSettings()
    {
        using var context = CreateContext();
        var otherDealerId = Guid.NewGuid();
        var mine = SeedSettings(context);
        var theirs = new DealerSettingsEntity(otherDealerId, "Other Dealer", "other@test.com");
        context.DealerSettings.Add(theirs);
        await context.SaveChangesAsync();

        var handler = new UpdateDealerLandingCommandHandler(context, MockTenant(TestDealerId).Object);
        await handler.Handle(
            new UpdateDealerLandingCommand("Sólo la mía", null, null, null, null),
            CancellationToken.None);

        var untouched = await context.DealerSettings
            .IgnoreQueryFilters()
            .SingleAsync(s => s.DealerId == otherDealerId);
        untouched.HistoryText.Should().BeNull();
    }
}
