using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Cars.Commands.BackfillPrePhase2Images;
using Domain.Cars;
using Domain.Cars.Attributes;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace Application.UnitTests.Cars.Commands.BackfillPrePhase2Images;

/// <summary>
/// RED tests for <see cref="BackfillPrePhase2ImagesCommandHandler"/>.
/// Covers REQ-FVIP-1 in the spec: dry-run, apply, idempotency, tenant scoping.
///
/// TDD note (Slice 1, Task 1.2-1.4): these three test methods are the consolidated
/// red phase for the backfill handler. They live in one file because they share
/// fixture setup; the proposal lists them as 3 separate commits, but they are one
/// deliverable (one test file, one handler under test).
/// </summary>
public class BackfillPrePhase2ImagesCommandHandlerTests
{
    private static readonly Guid DealerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ActorUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static (TestApplicationDbContext context, Mock<ICurrentTenantService> tenant, Mock<IUserContext> user)
        BuildContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new TestApplicationDbContext(options);

        var tenant = new Mock<ICurrentTenantService>();
        tenant.Setup(t => t.DealerId).Returns(DealerId);
        tenant.Setup(t => t.HasTenant).Returns(true);

        var user = new Mock<IUserContext>();
        user.Setup(u => u.UserId).Returns(ActorUserId);

        return (context, tenant, user);
    }

    private static Car SeedCarWithLegacyImage(TestApplicationDbContext context, Guid dealerId)
    {
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        var car = new Car(
            dealerId,
            marca,
            modelo,
            Color.Black,
            TypeCar.Sedan,
            StatusCar.New,
            StatusServiceCar.Disponible,
            4, 5, 2000, 0, 2024,
            "LEGACY1",
            "Legacy car with no cover",
            15000m,
            new DateTime(2024, 1, 1));

        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.SaveChanges();

        // Legacy image: IsCover = false, ObjectKey = null. This is the "no cover" case
        // produced by the pre-Phase-2 upload handler when it didn't force IsPrimary=true
        // on the first image.
        var legacyImage = new CarImage(
            car.Id,
            imageUrl: "https://legacy.example.com/cars/old-image.jpg",
            isCover: false,
            displayOrder: 0);
        context.CarImages.Add(legacyImage);
        context.SaveChanges();

        return car;
    }

    [Fact]
    public async Task Handle_DryRun_ReturnsAffectedRowCount_LeavesImagesUnchanged_WritesDryRunAudit()
    {
        // Arrange
        var (context, tenant, user) = BuildContext();
        SeedCarWithLegacyImage(context, DealerId);
        var dateProvider = new FakeDateTimeProvider { UtcNow = new DateTime(2026, 6, 7, 12, 0, 0, DateTimeKind.Utc) };
        var handler = new BackfillPrePhase2ImagesCommandHandler(context, tenant.Object, user.Object, dateProvider);

        var command = new BackfillPrePhase2ImagesCommand(DryRun: true, Confirmed: false);

        // Act
        Result<BackfillPrePhase2ImagesResult> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AffectedRowCount.Should().Be(1);
        result.Value.Action.Should().Be(BackfillAction.DryRun);

        // The legacy car_image row MUST NOT have been mutated.
        CarImage image = context.CarImages.IgnoreQueryFilters().Single();
        image.IsCover.Should().BeFalse();
        image.ObjectKey.Should().BeNull();

        // Exactly one backfill_audit row was inserted with action='dry-run'.
        List<BackfillAudit> audits = context.Set<BackfillAudit>().IgnoreQueryFilters().ToList();
        audits.Should().HaveCount(1);
        audits[0].Action.Should().Be(BackfillAction.DryRun);
        audits[0].ActorUserId.Should().Be(ActorUserId);
        audits[0].DealerId.Should().Be(DealerId);
        audits[0].AffectedRowCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ApplyWithConfirmedTrue_SetsCoverTrueOnFirstImage_WritesApplyAudit()
    {
        // Arrange
        var (context, tenant, user) = BuildContext();
        SeedCarWithLegacyImage(context, DealerId);
        var dateProvider = new FakeDateTimeProvider { UtcNow = new DateTime(2026, 6, 7, 12, 0, 0, DateTimeKind.Utc) };
        var handler = new BackfillPrePhase2ImagesCommandHandler(context, tenant.Object, user.Object, dateProvider);

        var command = new BackfillPrePhase2ImagesCommand(DryRun: false, Confirmed: true);

        // Act
        Result<BackfillPrePhase2ImagesResult> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AffectedRowCount.Should().Be(1);
        result.Value.Action.Should().Be(BackfillAction.Apply);

        // The legacy car_image row is now cover.
        CarImage image = context.CarImages.IgnoreQueryFilters().Single();
        image.IsCover.Should().BeTrue();

        // Audit row recorded.
        List<BackfillAudit> audits = context.Set<BackfillAudit>().IgnoreQueryFilters().ToList();
        audits.Should().HaveCount(1);
        audits[0].Action.Should().Be(BackfillAction.Apply);
    }

    [Fact]
    public async Task Handle_ApplyTwice_IsIdempotent_SecondCallReturnsZeroAffected_ButAppendsAudit()
    {
        // Arrange
        var (context, tenant, user) = BuildContext();
        SeedCarWithLegacyImage(context, DealerId);
        var dateProvider = new FakeDateTimeProvider { UtcNow = new DateTime(2026, 6, 7, 12, 0, 0, DateTimeKind.Utc) };
        var handler = new BackfillPrePhase2ImagesCommandHandler(context, tenant.Object, user.Object, dateProvider);

        // Act — first apply
        Result<BackfillPrePhase2ImagesResult> first =
            await handler.Handle(new BackfillPrePhase2ImagesCommand(false, true), CancellationToken.None);

        // Act — second apply (idempotent)
        Result<BackfillPrePhase2ImagesResult> second =
            await handler.Handle(new BackfillPrePhase2ImagesCommand(false, true), CancellationToken.None);

        // Assert
        first.Value.AffectedRowCount.Should().Be(1);
        second.Value.AffectedRowCount.Should().Be(0);
        second.IsSuccess.Should().BeTrue();

        // Audit table is append-only — one row per call, no matter the outcome.
        List<BackfillAudit> audits = context.Set<BackfillAudit>().IgnoreQueryFilters().ToList();
        audits.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_RespectsTenantScope_OnlyTouchesCurrentTenantCars()
    {
        // Arrange — two cars in different dealers, only one matches the resolved tenant.
        var (context, tenant, user) = BuildContext();
        var otherDealerId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        SeedCarWithLegacyImage(context, DealerId);
        var otherCar = SeedCarWithLegacyImage(context, otherDealerId);

        var dateProvider = new FakeDateTimeProvider();
        var handler = new BackfillPrePhase2ImagesCommandHandler(context, tenant.Object, user.Object, dateProvider);

        // Act
        Result<BackfillPrePhase2ImagesResult> result =
            await handler.Handle(new BackfillPrePhase2ImagesCommand(false, true), CancellationToken.None);

        // Assert — only the current tenant's car was affected.
        result.Value.AffectedRowCount.Should().Be(1);

        List<CarImage> allImages = context.CarImages.IgnoreQueryFilters().ToList();
        allImages.Should().HaveCount(2);
        allImages.Single(i => i.CarId == otherCar.Id).IsCover.Should().BeFalse(
            "the other tenant's image must not be touched by the backfill");
        allImages.Single(i => i.CarId != otherCar.Id).IsCover.Should().BeTrue();
    }
}
