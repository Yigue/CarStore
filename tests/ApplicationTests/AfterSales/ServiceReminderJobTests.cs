using Application.Abstractions.Tenancy;
using Application.AfterSales;
using Domain.AfterSales;
using Domain.Clients;
using Domain.Financial.Attributes;
using Domain.Sales;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.AfterSales;

/// <summary>
/// sfc C2.3/C2.4 / RN-049 (after-sales): service reminders follow the dealer cadence (6 months by default) from the
/// delivery date; a warranty that already ended produces no warranty reminder; the client's contact consent is honoured.
/// </summary>
public class ServiceReminderJobTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime Delivered = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // No dealer on the context: the sweep runs platform-wide.
    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(Sale Sale, Client Client)> SeedDeliveredSaleAsync(
        TestApplicationDbContext context, bool consent = true, bool completed = true, DateTime? delivered = null)
    {
        if (!await context.DealerSettings.AnyAsync())
            context.DealerSettings.Add(new Domain.DealerSettings.DealerSettings(DealerId, "Autos Sur", "sur@dealer.com"));
        var client = new Client(DealerId, "Ana", "Perez", "30111222", "ana@x.com", "1", "Addr", Delivered.AddDays(-10));
        client.SetServiceReminderConsent(consent);
        context.Clients.Add(client);
        await context.SaveChangesAsync();
        var sale = new Sale(DealerId, Guid.NewGuid(), client.Id, 10000m, PaymentMethod.Cash, "C-1", "c", Delivered.AddDays(-5));
        if (completed)
            sale.Complete();
        sale.SetLegalDocuments(null, null, null, delivered ?? Delivered);
        context.Sales.Add(sale);
        await context.SaveChangesAsync();
        return (sale, client);
    }

    private static GenerateServiceRemindersCommandHandler Handler(TestApplicationDbContext context, DateTime now) =>
        new(context, new FakeDateTimeProvider { UtcNow = now });

    [Fact]
    public async Task Six_months_after_delivery_a_service_reminder_is_generated_once()
    {
        using var context = CreateContext();
        var (sale, _) = await SeedDeliveredSaleAsync(context);

        var first = await Handler(context, new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc)).Handle(new GenerateServiceRemindersCommand(), CancellationToken.None);
        var again = await Handler(context, new DateTime(2026, 7, 2, 9, 0, 0, DateTimeKind.Utc)).Handle(new GenerateServiceRemindersCommand(), CancellationToken.None);

        first.Value.Should().Be(1);
        again.Value.Should().Be(0);
        var reminder = await context.ServiceReminders.SingleAsync();
        reminder.SaleId.Should().Be(sale.Id);
        reminder.Kind.Should().Be(ServiceReminderKind.PeriodicService);
        reminder.DueOnUtc.Should().Be(Delivered.AddMonths(6));
        reminder.ClientContactAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task Before_the_cadence_nothing_is_generated()
    {
        using var context = CreateContext();
        await SeedDeliveredSaleAsync(context);

        var result = await Handler(context, Delivered.AddMonths(5)).Handle(new GenerateServiceRemindersCommand(), CancellationToken.None);

        result.Value.Should().Be(0);
    }

    [Fact]
    public async Task An_active_warranty_close_to_its_end_gets_one_reminder_but_an_ended_one_gets_none()
    {
        using var context = CreateContext();
        var (activeSale, client) = await SeedDeliveredSaleAsync(context);
        var (endedSale, _) = await SeedDeliveredSaleAsync(context, delivered: Delivered.AddMonths(-12));
        context.WarrantyRecords.AddRange(
            WarrantyRecord.Register(DealerId, activeSale.Id, Guid.NewGuid(), client.Id, Delivered, 3, null).Value,
            WarrantyRecord.Register(DealerId, endedSale.Id, Guid.NewGuid(), client.Id, Delivered.AddMonths(-12), 3, null).Value);
        await context.SaveChangesAsync();
        var now = Delivered.AddMonths(3).AddDays(-10);

        await Handler(context, now).Handle(new GenerateServiceRemindersCommand(), CancellationToken.None);

        var warrantyReminders = await context.ServiceReminders.Where(r => r.Kind == ServiceReminderKind.WarrantyExpiring).ToListAsync();
        warrantyReminders.Should().ContainSingle().Which.SaleId.Should().Be(activeSale.Id);
    }

    [Fact]
    public async Task Client_without_consent_is_recorded_as_not_contactable()
    {
        using var context = CreateContext();
        await SeedDeliveredSaleAsync(context, consent: false);

        await Handler(context, Delivered.AddMonths(6)).Handle(new GenerateServiceRemindersCommand(), CancellationToken.None);

        (await context.ServiceReminders.SingleAsync()).ClientContactAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task A_sale_that_is_not_completed_gets_nothing()
    {
        using var context = CreateContext();
        await SeedDeliveredSaleAsync(context, completed: false);

        var result = await Handler(context, Delivered.AddMonths(7)).Handle(new GenerateServiceRemindersCommand(), CancellationToken.None);

        result.Value.Should().Be(0);
    }
}
