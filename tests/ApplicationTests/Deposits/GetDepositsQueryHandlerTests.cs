using Application.Abstractions.Tenancy;
using Application.Deposits.Get;
using Domain.Cars.Attributes;
using Domain.Deposits;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Deposits;

/// <summary>sfc B1.3.5: GET deposits lists the caller's dealer only, optionally for one car, newest first.</summary>
public class GetDepositsQueryHandlerTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    private static Deposit NewDeposit(Guid dealerId, Guid carId, DateTime createdAt) =>
        Deposit.Create(dealerId, carId, Guid.NewGuid(), null, 1000m, "ARS", null, createdAt,
            new DepositCarContext(StatusServiceCar.Disponible, null, false, "ARS")).Value;

    [Fact]
    public async Task Handle_Should_ListTheCallersDeposits_FilteredByCar_NewestFirst()
    {
        using var context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var carId = Guid.NewGuid();
        var older = NewDeposit(DealerId, carId, Now.AddDays(-20));
        older.Expire(Now.AddDays(-10));
        var newer = NewDeposit(DealerId, carId, Now.AddDays(-1));
        context.Deposits.AddRange(older, newer, NewDeposit(DealerId, Guid.NewGuid(), Now), NewDeposit(Guid.NewGuid(), carId, Now));
        await context.SaveChangesAsync();
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);

        var result = await new GetDepositsQueryHandler(context, tenant.Object).Handle(new GetDepositsQuery(carId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(d => d.Id).Should().Equal(newer.Id, older.Id);
        result.Value[0].Status.Should().Be("Active");
        result.Value[0].Currency.Should().Be("ARS");
    }
}
