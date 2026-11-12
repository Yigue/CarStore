using Application.Abstractions.Email;
using Application.AfterSales;
using Domain.AfterSales;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Financial.Attributes;
using Domain.Sales;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Application.UnitTests.AfterSales;

/// <summary>
/// sfc C2.4.2 / RN-047: a due reminder is emailed to the client exactly once, only with consent and a usable email;
/// a transport failure leaves it unnotified so the next run retries.
/// </summary>
public class ServiceReminderEmailTests
{
    private static readonly Guid DealerA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DealerB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ITransactionalEmailSender> _email = new();

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private NotifyServiceRemindersCommandHandler Handler(TestApplicationDbContext context) =>
        new(context, new FakeDateTimeProvider { UtcNow = Now }, _email.Object, NullLogger<NotifyServiceRemindersCommandHandler>.Instance);

    private static int _plate = 100;
    private static string NextPlate() => "ABC" + Interlocked.Increment(ref _plate);

    private static async Task<ServiceReminder> SeedReminderAsync(
        TestApplicationDbContext context, Guid dealerId, string dealerName, bool consent = true, string email = "ana@x.com",
        ServiceReminderKind kind = ServiceReminderKind.PeriodicService, DateTime? due = null)
    {
        context.DealerSettings.Add(new Domain.DealerSettings.DealerSettings(dealerId, dealerName, "d@dealer.com"));
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        var car = new Car(dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 1600, 1000, 2021, NextPlate(), "desc", 10000m, Now.AddDays(-300));
        var client = new Client(dealerId, "Ana", "Perez", "30111222", email, "111", "Addr", Now.AddDays(-300));
        client.SetServiceReminderConsent(consent);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.Add(client);
        await context.SaveChangesAsync(); // ids are generated on save
        var sale = new Sale(dealerId, car.Id, client.Id, 10000m, PaymentMethod.Cash, "C-1", "c", Now.AddDays(-250));
        context.Sales.Add(sale);
        await context.SaveChangesAsync();
        var reminder = ServiceReminder.Create(dealerId, sale.Id, client.Id, kind, due ?? Now.AddDays(-1), consent, Now.AddDays(-1));
        context.ServiceReminders.Add(reminder);
        await context.SaveChangesAsync();
        return reminder;
    }

    [Fact]
    public async Task Consenting_client_gets_one_email_and_the_second_run_sends_nothing()
    {
        using var context = CreateContext();
        var reminder = await SeedReminderAsync(context, DealerA, "Autos Sur");

        var first = await Handler(context).Handle(new NotifyServiceRemindersCommand(), CancellationToken.None);
        var second = await Handler(context).Handle(new NotifyServiceRemindersCommand(), CancellationToken.None);

        first.Value.Should().Be(1);
        second.Value.Should().Be(0);
        _email.Verify(e => e.SendAsync(EmailTemplate.ServiceReminder, "ana@x.com", It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
        (await context.ServiceReminders.SingleAsync(r => r.Id == reminder.Id)).NotifiedAtUtc.Should().Be(Now);
    }

    [Fact]
    public async Task Client_without_consent_gets_no_email_and_stays_unnotified()
    {
        using var context = CreateContext();
        await SeedReminderAsync(context, DealerA, "Autos Sur", consent: false);

        var result = await Handler(context).Handle(new NotifyServiceRemindersCommand(), CancellationToken.None);

        result.Value.Should().Be(0);
        _email.VerifyNoOtherCalls();
        (await context.ServiceReminders.SingleAsync()).NotifiedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Consent_granted_later_lets_a_still_due_reminder_be_emailed()
    {
        using var context = CreateContext();
        await SeedReminderAsync(context, DealerA, "Autos Sur", consent: false);
        await Handler(context).Handle(new NotifyServiceRemindersCommand(), CancellationToken.None);
        (await context.Clients.SingleAsync()).SetServiceReminderConsent(true);
        await context.SaveChangesAsync();

        var result = await Handler(context).Handle(new NotifyServiceRemindersCommand(), CancellationToken.None);

        result.Value.Should().Be(1);
    }

    [Fact]
    public async Task Transport_failure_does_not_mark_the_reminder_and_the_next_run_retries()
    {
        using var context = CreateContext();
        await SeedReminderAsync(context, DealerA, "Autos Sur");
        _email.SetupSequence(e => e.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"))
            .Returns(Task.CompletedTask);

        var failed = await Handler(context).Handle(new NotifyServiceRemindersCommand(), CancellationToken.None);
        (await context.ServiceReminders.SingleAsync()).NotifiedAtUtc.Should().BeNull();
        var retried = await Handler(context).Handle(new NotifyServiceRemindersCommand(), CancellationToken.None);

        failed.Value.Should().Be(0);
        retried.Value.Should().Be(1);
        (await context.ServiceReminders.SingleAsync()).NotifiedAtUtc.Should().Be(Now);
    }

    [Fact]
    public async Task A_failure_on_one_reminder_does_not_stop_the_rest_of_the_batch()
    {
        using var context = CreateContext();
        await SeedReminderAsync(context, DealerA, "Autos Sur", email: "bad@x.com");
        await SeedReminderAsync(context, DealerB, "Autos Norte", email: "ok@x.com");
        _email.Setup(e => e.SendAsync(It.IsAny<EmailTemplate>(), "bad@x.com", It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await Handler(context).Handle(new NotifyServiceRemindersCommand(), CancellationToken.None);

        result.Value.Should().Be(1);
    }

    [Fact]
    public async Task Each_dealers_reminders_use_their_own_dealer_name()
    {
        using var context = CreateContext();
        await SeedReminderAsync(context, DealerA, "Autos Sur", email: "a@x.com");
        await SeedReminderAsync(context, DealerB, "Autos Norte", email: "b@x.com");
        var seen = new Dictionary<string, string>();
        _email.Setup(e => e.SendAsync(EmailTemplate.ServiceReminder, It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<EmailTemplate, string, IReadOnlyDictionary<string, string>, CancellationToken>((_, to, model, _) => seen[to] = model["DealerName"])
            .Returns(Task.CompletedTask);

        await Handler(context).Handle(new NotifyServiceRemindersCommand(), CancellationToken.None);

        seen["a@x.com"].Should().Be("Autos Sur");
        seen["b@x.com"].Should().Be("Autos Norte");
    }

    [Fact]
    public async Task A_warranty_reminder_whose_warranty_already_ended_is_not_emailed()
    {
        using var context = CreateContext();
        await SeedReminderAsync(context, DealerA, "Autos Sur", kind: ServiceReminderKind.WarrantyExpiring, due: Now.AddDays(-2));
        await SeedReminderAsync(context, DealerB, "Autos Norte", kind: ServiceReminderKind.WarrantyExpiring, due: Now.AddDays(10));

        var result = await Handler(context).Handle(new NotifyServiceRemindersCommand(), CancellationToken.None);

        result.Value.Should().Be(1);
    }
}
