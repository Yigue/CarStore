using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Leads.UpdateStatus;
using Domain.Appointments;
using Domain.Clients;
using Domain.Clients.Attributes;
using Domain.Leads;
using Domain.Sales;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Leads;

/// <summary>
/// A stage names something that happened, so it cannot be reached before that thing exists.
/// Demostración needs a booked appointment, Negociación a quote, Ganado a sale.
///
/// Without these rules a cancelled form left the lead filed under an event nobody could find —
/// negotiating with no number on the table, or demoing a car nobody scheduled — because the stage
/// had already changed by the time the form appeared.
/// </summary>
public class UpdateLeadStatusWonRequiresSaleTests
{
    private static readonly Guid DealerId = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    /// <summary>A lead already advanced to Negociación — the only stage Ganado follows.</summary>
    private static async Task<Lead> SeedLeadInNegociacionAsync(TestApplicationDbContext context)
    {
        var lead = Lead.Create(
            DealerId, "Ana Fernandez", "ana@test.com", "1", LeadSource.Web, DateTime.UtcNow);
        lead.LinkVehicle(Guid.NewGuid());
        lead.UpdateStatus(LeadStatus.Contactado, "primer contacto");
        lead.UpdateStatus(LeadStatus.Demostracion, null);
        lead.UpdateStatus(LeadStatus.Negociacion, null);

        context.Leads.Add(lead);
        await context.SaveChangesAsync();
        return lead;
    }

    private static Sale BuildSale(Guid clientId, Guid? leadId) =>
        new(DealerId, Guid.NewGuid(), clientId, 15000m,
            Domain.Financial.Attributes.PaymentMethod.Cash,
            contractNumber: "C-1", comments: string.Empty, saleDate: DateTime.UtcNow, leadId: leadId);

    private static UpdateLeadStatusCommand WinCommand(Guid leadId) =>
        new(leadId, LeadStatus.Ganado, null, null);

    [Fact]
    public async Task Handle_Should_Refuse_WhenNoSaleBacksTheLead()
    {
        using var context = CreateContext();
        Lead lead = await SeedLeadInNegociacionAsync(context);

        var result = await new UpdateLeadStatusCommandHandler(context)
            .Handle(WinCommand(lead.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Leads.WonRequiresSale");

        (await context.Leads.SingleAsync()).Status.Should().Be(
            LeadStatus.Negociacion, "a refused transition must leave the lead where it was");
    }

    [Fact]
    public async Task Handle_Should_Allow_WhenASaleIsBookedAgainstTheLead()
    {
        using var context = CreateContext();
        Lead lead = await SeedLeadInNegociacionAsync(context);
        context.Sales.Add(BuildSale(Guid.NewGuid(), lead.Id));
        await context.SaveChangesAsync();

        var result = await new UpdateLeadStatusCommandHandler(context)
            .Handle(WinCommand(lead.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.Leads.SingleAsync()).Status.Should().Be(LeadStatus.Ganado);
    }

    /// <summary>
    /// The sale is often booked against the client the lead was converted into, not the lead
    /// itself. Both mean the deal closed.
    /// </summary>
    [Fact]
    public async Task Handle_Should_Allow_WhenTheSaleIsBookedAgainstTheConvertedClient()
    {
        using var context = CreateContext();
        Lead lead = await SeedLeadInNegociacionAsync(context);

        var client = new Client(DealerId, "Ana", "Fernandez", "1", "ana@test.com", "2", "Addr",
            DateTime.UtcNow, ClientType.Individual, originLeadId: lead.Id);
        context.Clients.Add(client);
        context.Sales.Add(BuildSale(client.Id, leadId: null));
        await context.SaveChangesAsync();

        var result = await new UpdateLeadStatusCommandHandler(context)
            .Handle(WinCommand(lead.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// ForceStatus is the system-driven path — accepting a quote auto-advances the lead — and it
    /// already documents that it bypasses the sequential rules the UI enforces. This rule guards
    /// the user-driven command only; extending it to ForceStatus would break quote acceptance.
    /// </summary>
    [Fact]
    public void ForceStatus_Should_StillReachGanado_WithoutASale()
    {
        var lead = Lead.Create(
            DealerId, "Ana Fernandez", "ana@test.com", "1", LeadSource.Web, DateTime.UtcNow);

        Action act = () => lead.ForceStatus(LeadStatus.Ganado);

        act.Should().NotThrow();
        lead.Status.Should().Be(LeadStatus.Ganado);
    }

    // ─── Demostración needs its appointment ───────────────────────────────────

    private static async Task<Lead> SeedLeadInContactadoAsync(TestApplicationDbContext context)
    {
        var lead = Lead.Create(
            DealerId, "Ana Fernandez", "ana@test.com", "1", LeadSource.Web, DateTime.UtcNow);
        lead.LinkVehicle(Guid.NewGuid());
        lead.UpdateStatus(LeadStatus.Contactado, "primer contacto");

        context.Leads.Add(lead);
        await context.SaveChangesAsync();
        return lead;
    }

    [Fact]
    public async Task Handle_Should_Refuse_Demostracion_WhenNoAppointmentIsBooked()
    {
        using var context = CreateContext();
        Lead lead = await SeedLeadInContactadoAsync(context);

        var result = await new UpdateLeadStatusCommandHandler(context).Handle(
            new UpdateLeadStatusCommand(lead.Id, LeadStatus.Demostracion, null, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Leads.DemoRequiresAppointment");

        (await context.Leads.SingleAsync()).Status.Should().Be(
            LeadStatus.Contactado, "abandoning the appointment form must leave the lead put");
    }

    [Fact]
    public async Task Handle_Should_Allow_Demostracion_WhenAnAppointmentExists()
    {
        using var context = CreateContext();
        Lead lead = await SeedLeadInContactadoAsync(context);

        context.Appointments.Add(Appointment.Create(
            dealerId: DealerId,
            vehicleId: Guid.NewGuid(),
            clientId: null,
            leadId: lead.Id,
            agentId: Guid.NewGuid(),
            start: DateTime.UtcNow.AddDays(1),
            end: DateTime.UtcNow.AddDays(1).AddHours(1),
            type: AppointmentType.TestDrive,
            notes: null,
            createdAtUtc: DateTime.UtcNow));
        await context.SaveChangesAsync();

        var result = await new UpdateLeadStatusCommandHandler(context).Handle(
            new UpdateLeadStatusCommand(lead.Id, LeadStatus.Demostracion, null, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ─── Negociación needs its quote ──────────────────────────────────────────

    [Fact]
    public async Task Handle_Should_Refuse_Negociacion_WhenNoQuoteExists()
    {
        using var context = CreateContext();
        Lead lead = await SeedLeadInContactadoAsync(context);
        lead.UpdateStatus(LeadStatus.Demostracion, null);
        await context.SaveChangesAsync();

        var result = await new UpdateLeadStatusCommandHandler(context).Handle(
            new UpdateLeadStatusCommand(lead.Id, LeadStatus.Negociacion, null, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Leads.NegotiationRequiresQuote");

        (await context.Leads.SingleAsync()).Status.Should().Be(LeadStatus.Demostracion);
    }

    /// <summary>
    /// Contactado carries its requirement — notes — in the request itself, so it is reachable
    /// without any external record and must stay that way.
    /// </summary>
    [Fact]
    public async Task Handle_Should_NotAffectOtherTransitions()
    {
        using var context = CreateContext();
        var lead = Lead.Create(
            DealerId, "Ana Fernandez", "ana@test.com", "1", LeadSource.Web, DateTime.UtcNow);
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        var result = await new UpdateLeadStatusCommandHandler(context).Handle(
            new UpdateLeadStatusCommand(lead.Id, LeadStatus.Contactado, "primer contacto", null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
