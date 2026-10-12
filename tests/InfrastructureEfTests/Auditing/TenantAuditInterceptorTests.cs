using Application.Abstractions.Auditing;
using Application.Abstractions.Authentication;
using Application.Abstractions.Tenancy;
using Domain.Auditing;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Deposits;
using Domain.Financial.Attributes;
using Domain.Sales;
using Domain.Sales.Attributes;
using FluentAssertions;
using Infrastructure.Database;
using Infrastructure.Database.Interceptors;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using Xunit;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace InfrastructureEfTests.Auditing;

/// <summary>sfc B2.2 / ADR C10 / RN-025: the interceptor writes a DealerAuditLogEntry, same SaveChanges, for allow-listed changes.</summary>
public class TenantAuditInterceptorTests
{
    private static readonly Guid DealerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ActorId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTime Now = new(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);

    private sealed class FakeTenant : ICurrentTenantService
    {
        public Guid DealerId => TenantAuditInterceptorTests.DealerId;
        public bool HasTenant => false;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class FakeUser : IUserContext
    {
        public Guid UserId => ActorId;
    }

    private sealed class FakeAuditContext : IAuditContext
    {
        public string? Reason { get; private set; }
        public string CorrelationId => "corr-1";
        public void SetReason(string? reason) => Reason = reason;
    }

    private sealed class FakeClock : IDateTimeProvider
    {
        public DateTime UtcNow => Now;
    }

    private static ApplicationDbContext NewContext(SqliteConnection connection, FakeAuditContext audit)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new TenantAuditInterceptor(new FakeUser(), audit, new FakeClock()))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        return new ApplicationDbContext(options, new NoOpPublisher(), new FakeTenant());
    }

    private static async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:;Foreign Keys=False");
        await connection.OpenAsync();
        await using var ctx = NewContext(connection, new FakeAuditContext());
        await ctx.Database.EnsureCreatedAsync();
        return connection;
    }

    private static Car NewCar() => new(DealerId, new Marca("Toyota"), new Modelo("Corolla", Guid.NewGuid()),
        Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2020, "AB123CD",
        "desc", 10000m, Now);

    private static Sale NewCompletedSale()
    {
        var sale = new Sale(DealerId, Guid.NewGuid(), Guid.NewGuid(), 10000m, PaymentMethod.Cash, "CN-1", "", Now);
        sale.Complete();
        return sale;
    }

    [Fact]
    public async Task CarPriceChange_WritesAuditEntry_WithActorReasonAndValues()
    {
        await using var connection = await OpenAsync();
        var car = NewCar();
        await using (var seed = NewContext(connection, new FakeAuditContext()))
        {
            seed.Cars.Add(car);
            await seed.SaveChangesAsync();
        }

        var audit = new FakeAuditContext();
        await using (var ctx = NewContext(connection, audit))
        {
            var tracked = await ctx.Cars.SingleAsync();
            audit.SetReason("Ajuste de lista");
            tracked.UpdatePrice(12500m, Now);
            await ctx.SaveChangesAsync();
        }

        await using var read = NewContext(connection, new FakeAuditContext());
        var entry = await read.DealerAuditLogs.AsNoTracking().SingleAsync();
        entry.EntityType.Should().Be("Car");
        entry.EntityId.Should().Be(car.Id);
        entry.Property.Should().Be("Price");
        entry.OldValue.Should().Be("10000 ARS");
        entry.NewValue.Should().Be("12500 ARS");
        entry.ActorUserId.Should().Be(ActorId);
        entry.Reason.Should().Be("Ajuste de lista");
        entry.CorrelationId.Should().Be("corr-1");
        entry.DealerId.Should().Be(DealerId);
        entry.OccurredAtUtc.Should().Be(Now);
    }

    [Fact]
    public async Task SaleStatusChange_WritesAuditEntry_WithBusinessReason()
    {
        await using var connection = await OpenAsync();
        var sale = NewCompletedSale();
        await using (var seed = NewContext(connection, new FakeAuditContext()))
        {
            seed.Sales.Add(sale);
            await seed.SaveChangesAsync();
        }

        var audit = new FakeAuditContext();
        await using (var ctx = NewContext(connection, audit))
        {
            var tracked = await ctx.Sales.SingleAsync();
            audit.SetReason("Sale.Void: cliente desistio");
            tracked.Void("cliente desistio", ActorId, Now).IsSuccess.Should().BeTrue();
            await ctx.SaveChangesAsync();
        }

        await using var read = NewContext(connection, new FakeAuditContext());
        var entry = await read.DealerAuditLogs.AsNoTracking().SingleAsync();
        entry.EntityType.Should().Be("Sale");
        entry.EntityId.Should().Be(sale.Id);
        entry.Property.Should().Be("Status");
        entry.OldValue.Should().Be("Completed");
        entry.NewValue.Should().Be("Voided");
        entry.Reason.Should().Be("Sale.Void: cliente desistio");
        entry.ActorUserId.Should().Be(ActorId);
    }

    [Fact]
    public async Task ChangeOutsideTheAllowList_WritesNothing()
    {
        await using var connection = await OpenAsync();
        await using (var seed = NewContext(connection, new FakeAuditContext()))
        {
            seed.Cars.Add(NewCar());
            await seed.SaveChangesAsync();
        }

        await using (var ctx = NewContext(connection, new FakeAuditContext()))
        {
            var tracked = await ctx.Cars.SingleAsync();
            tracked.Reserve(Now); // StatusService is not in the allow-list
            await ctx.SaveChangesAsync();
        }

        await using var read = NewContext(connection, new FakeAuditContext());
        (await read.DealerAuditLogs.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task InsertingAuditedEntities_WritesNoEntry()
    {
        await using var connection = await OpenAsync();
        await using (var ctx = NewContext(connection, new FakeAuditContext()))
        {
            ctx.Cars.Add(NewCar());
            ctx.Sales.Add(NewCompletedSale());
            await ctx.SaveChangesAsync();
        }

        await using var read = NewContext(connection, new FakeAuditContext());
        (await read.DealerAuditLogs.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task EntryIsWrittenInTheSameTransaction_RolledBackWithTheChange()
    {
        await using var connection = await OpenAsync();
        await using (var seed = NewContext(connection, new FakeAuditContext()))
        {
            seed.Cars.Add(NewCar());
            await seed.SaveChangesAsync();
        }

        await using (var ctx = NewContext(connection, new FakeAuditContext()))
        {
            await using var tx = await ctx.Database.BeginTransactionAsync();
            var tracked = await ctx.Cars.SingleAsync();
            tracked.UpdatePrice(99999m, Now);
            await ctx.SaveChangesAsync();
            (await ctx.DealerAuditLogs.CountAsync()).Should().Be(1, "the entry is part of the open transaction");
            await tx.RollbackAsync();
        }

        await using var read = NewContext(connection, new FakeAuditContext());
        (await read.DealerAuditLogs.AsNoTracking().CountAsync()).Should().Be(0);
        (await read.Cars.AsNoTracking().SingleAsync()).Price.Amount.Should().Be(10000m);
    }

    [Fact]
    public void AllowList_CoversCarPrice_SaleStatus_AndEveryDepositProperty()
    {
        TenantAuditInterceptor.IsAudited(typeof(Car), "Price").Should().BeTrue();
        TenantAuditInterceptor.IsAudited(typeof(Sale), "Status").Should().BeTrue();
        TenantAuditInterceptor.IsAudited(typeof(Deposit), "Status").Should().BeTrue();
        TenantAuditInterceptor.IsAudited(typeof(Deposit), "RefundedAmount").Should().BeTrue();
        TenantAuditInterceptor.IsAudited(typeof(Car), "Description").Should().BeFalse();
        TenantAuditInterceptor.IsAudited(typeof(Sale), "Comments").Should().BeFalse();
    }

    [Fact]
    public void AuditEntry_IsASeparateTableFromThePlatformLog()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        using var ctx = NewContext(connection, new FakeAuditContext());
        ctx.Model.FindEntityType(typeof(DealerAuditLogEntry))!.GetTableName().Should().Be("dealer_audit_logs");
    }
}
