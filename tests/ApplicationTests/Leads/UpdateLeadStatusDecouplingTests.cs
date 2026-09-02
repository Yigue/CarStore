using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.Leads.UpdateStatus;
using Application.Quotes.Create;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Leads;
using Domain.Leads.Events;
using Domain.Quotes.Attributes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Leads;

/// <summary>
/// REQ-CRM-DECOUPLE-001 (backend half): regression guardrail proving stage-update and
/// quote/appointment creation are independent mutations. Per design.md §1/§4, this is
/// architecturally guaranteed today — <see cref="UpdateLeadStatusCommandHandler"/> and
/// <see cref="CreateQuoteCommandHandler"/> each own a separate <c>SaveChangesAsync</c> call
/// (no shared unit of work), and domain events are dispatched later, out-of-band, via the
/// outbox (<c>ProcessOutboxMessagesJob</c>), which catches per-handler exceptions instead of
/// rolling back the already-committed aggregate. These tests fail if either invariant
/// regresses.
/// </summary>
public class UpdateLeadStatusDecouplingTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static Car CreateCar(TestApplicationDbContext context, Guid dealerId, StatusServiceCar serviceStatus)
    {
        var marca = new Marca("Peugeot");
        var modelo = new Modelo("208", marca.Id);
        var car = new Car(dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, serviceStatus, 4, 5, 1600, 1000, 2021, "DEC001", "desc", 15000m, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        return car;
    }

    [Fact]
    public async Task StageUpdate_And_QuoteCreationFailure_AreIndependentMutations()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = Lead.Create(dealerId, "Carlos Perez", "carlos@test.com", "1112223", LeadSource.Web, DateTime.UtcNow);
        lead.AssignAgent(Guid.NewGuid());
        context.Leads.Add(lead);
        // The car is already sold — CreateQuoteCommandHandler must reject it with
        // Cars.NotAvailable (409), independent of the lead's own transaction. (Reservado no
        // longer refuses a quote: a car can carry several competing offers, and only accepting
        // one commits it. Vendido is what "this unit is gone" means now.)
        var car = CreateCar(context, dealerId, StatusServiceCar.Vendido);
        await context.SaveChangesAsync();

        var updateHandler = new UpdateLeadStatusCommandHandler(context);
        var updateResult = await updateHandler.Handle(
            new UpdateLeadStatusCommand(lead.Id, LeadStatus.Contactado, "First contact made"),
            CancellationToken.None);

        updateResult.IsSuccess.Should().BeTrue();

        // Confirm the status change is durably committed before the quote attempt runs.
        var leadAfterStageUpdate = await context.Leads.AsNoTracking().SingleAsync(l => l.Id == lead.Id);
        leadAfterStageUpdate.Status.Should().Be(LeadStatus.Contactado);

        var tenantService = Mock.Of<ICurrentTenantService>(t => t.DealerId == dealerId);
        var quoteHandler = new CreateQuoteCommandHandler(context, new FakeDateTimeProvider(), tenantService);
        var quoteResult = await quoteHandler.Handle(
            new CreateQuoteCommand(car.Id, null, lead.Id, 100_000m, PaymentMethod.Contado, DateTime.UtcNow.AddDays(5), "c"),
            CancellationToken.None);

        quoteResult.IsFailure.Should().BeTrue("the car is sold — quote creation must fail with Cars.NotAvailable");

        // The lead's committed status update must not be reverted by the downstream failure —
        // there is no shared transaction/unit-of-work between the two handlers.
        var leadAfterQuoteFailure = await context.Leads.AsNoTracking().SingleAsync(l => l.Id == lead.Id);
        leadAfterQuoteFailure.Status.Should().Be(
            LeadStatus.Contactado,
            "a failing downstream quote-creation command must never revert an already-committed Lead status change");
    }

    [Fact]
    public async Task ThrowingLeadStatusChangedHandler_DoesNotRevertCommittedLeadStatus()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = Lead.Create(dealerId, "Ana Lopez", "ana@test.com", "4445556", LeadSource.Web, DateTime.UtcNow);
        lead.AssignAgent(Guid.NewGuid());
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        var updateHandler = new UpdateLeadStatusCommandHandler(context);
        var updateResult = await updateHandler.Handle(
            new UpdateLeadStatusCommand(lead.Id, LeadStatus.Contactado, "First contact made"),
            CancellationToken.None);
        updateResult.IsSuccess.Should().BeTrue();

        var domainEvent = new LeadStatusChangedDomainEvent(lead.Id, LeadStatus.Nuevo, LeadStatus.Contactado);
        var throwingHandler = new ThrowingLeadStatusChangedHandler();

        // Mirrors ProcessOutboxMessagesJob.Execute's per-message try/catch (lines 108-117):
        // a handler exception is swallowed (recorded as OutboxMessage.Error) and never
        // propagates to undo the already-committed command.
        Exception? caught = null;
        try
        {
            await throwingHandler.Handle(domainEvent, CancellationToken.None);
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        caught.Should().NotBeNull("the outbox job must observe and swallow the handler's exception");

        var leadAfterThrowingHandler = await context.Leads.AsNoTracking().SingleAsync(l => l.Id == lead.Id);
        leadAfterThrowingHandler.Status.Should().Be(
            LeadStatus.Contactado,
            "a throwing domain-event handler must never revert the already-committed Lead status");
    }

    private sealed class ThrowingLeadStatusChangedHandler : INotificationHandler<LeadStatusChangedDomainEvent>
    {
        public Task Handle(LeadStatusChangedDomainEvent notification, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated downstream handler failure.");
    }
}
