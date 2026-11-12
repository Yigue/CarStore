using Application.Abstractions.Tenancy;
using Application.AfterSales;
using Domain.Clients;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.AfterSales;

/// <summary>
/// sfc C2.4 / RN-047: the client's opt-in to service reminders is recorded explicitly (false by default), and only for a
/// client of the caller's own dealer.
/// </summary>
public class ServiceReminderConsentTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static (TestApplicationDbContext Context, SetServiceReminderConsentCommandHandler Handler) Setup()
    {
        var context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, DealerId);
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        return (context, new SetServiceReminderConsentCommandHandler(context, tenant.Object));
    }

    [Fact]
    public async Task Records_and_withdraws_the_consent()
    {
        var (context, handler) = Setup();
        var client = new Client(DealerId, "Ana", "Perez", "30111222", "ana@x.com", "1", "Addr", DateTime.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        (await handler.Handle(new SetServiceReminderConsentCommand(client.Id, true), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await context.Clients.SingleAsync()).AcceptsServiceReminders.Should().BeTrue();

        Application.Clients.Projections.ClientResponseMapper.Map(await context.Clients.SingleAsync())
            .AcceptsServiceReminders.Should().BeTrue("the client screen shows the current consent");

        await handler.Handle(new SetServiceReminderConsentCommand(client.Id, false), CancellationToken.None);
        (await context.Clients.SingleAsync()).AcceptsServiceReminders.Should().BeFalse();
    }

    [Fact]
    public async Task A_client_of_another_dealer_is_not_found()
    {
        var (context, handler) = Setup();
        var foreign = new Client(Guid.NewGuid(), "Ana", "Perez", "30111222", "ana@x.com", "1", "Addr", DateTime.UtcNow);
        context.Clients.Add(foreign);
        await context.SaveChangesAsync();

        var result = await handler.Handle(new SetServiceReminderConsentCommand(foreign.Id, true), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        (await context.Clients.SingleAsync()).AcceptsServiceReminders.Should().BeFalse();
    }
}
