using Application.Appointments.EventHandlers;
using Application.UnitTests;
using Domain.Appointments;
using Domain.Appointments.Events;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests.Appointments;

/// <summary>
/// Demo appointment automation (priority item 2): a TestDrive appointment linked to a Lead
/// should auto-advance that lead to LeadStatus.Demostracion, mirroring the system-driven
/// ForceStatus pattern used by AdvanceLeadOnQuoteCreatedHandler.
/// </summary>
public class AdvanceLeadOnDemoAppointmentCreatedHandlerTests
{
    private static readonly Guid DealerId = Guid.NewGuid();

    private TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestApplicationDbContext(options);
    }

    private static AdvanceLeadOnDemoAppointmentCreatedHandler CreateHandler(TestApplicationDbContext context) =>
        new(context, NullLogger<AdvanceLeadOnDemoAppointmentCreatedHandler>.Instance);

    private Lead SeedLead(TestApplicationDbContext context, LeadStatus status)
    {
        var lead = Lead.Create(DealerId, "Jane Doe", "jane@test.com", "555-1234", LeadSource.Web, DateTime.UtcNow);

        // Drive the lead to the requested status via the same forward-progression rules
        // the aggregate enforces, so tests exercise real reachable states.
        if (status is LeadStatus.Contactado or LeadStatus.Demostracion or LeadStatus.Negociacion or LeadStatus.Ganado)
        {
            lead.AssignAgent(Guid.NewGuid());
            lead.UpdateStatus(LeadStatus.Contactado, "first contact");
        }
        if (status is LeadStatus.Demostracion or LeadStatus.Negociacion or LeadStatus.Ganado)
        {
            lead.LinkVehicle(Guid.NewGuid());
            lead.UpdateStatus(LeadStatus.Demostracion, null);
        }
        if (status is LeadStatus.Negociacion or LeadStatus.Ganado)
        {
            lead.UpdateStatus(LeadStatus.Negociacion, null);
        }
        if (status is LeadStatus.Ganado)
        {
            lead.UpdateStatus(LeadStatus.Ganado, null);
        }
        if (status == LeadStatus.Perdido)
        {
            lead.UpdateStatus(LeadStatus.Perdido, null, LeadLossReason.Otro);
        }
        if (status == LeadStatus.Archivado)
        {
            lead.Archive();
        }

        context.Leads.Add(lead);
        context.SaveChanges();
        return lead;
    }

    private Appointment SeedAppointment(
        TestApplicationDbContext context,
        Guid? leadId,
        Guid? clientId,
        AppointmentType type)
    {
        var appointment = Appointment.Create(
            DealerId, Guid.NewGuid(), clientId, leadId, Guid.NewGuid(),
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(1),
            type, null, DateTime.UtcNow);

        context.Appointments.Add(appointment);
        context.SaveChanges();
        return appointment;
    }

    [Theory]
    [InlineData(LeadStatus.Nuevo)]
    [InlineData(LeadStatus.Contactado)]
    public async Task Handle_ShouldAdvanceLeadToDemostracion_WhenTestDriveAppointmentLinkedToEarlyStageLead(LeadStatus initialStatus)
    {
        var context = CreateContext();
        var lead = SeedLead(context, initialStatus);
        var appointment = SeedAppointment(context, lead.Id, null, AppointmentType.TestDrive);

        var handler = CreateHandler(context);
        await handler.Handle(new AppointmentCreatedDomainEvent(appointment.Id, appointment.AgentId, appointment.StartDateTime), CancellationToken.None);

        var updatedLead = await context.Leads.FirstAsync(l => l.Id == lead.Id);
        updatedLead.Status.Should().Be(LeadStatus.Demostracion);
    }

    [Fact]
    public async Task Handle_ShouldSkip_WhenAppointmentHasNoLeadId()
    {
        var context = CreateContext();
        var appointment = SeedAppointment(context, null, Guid.NewGuid(), AppointmentType.TestDrive);

        var handler = CreateHandler(context);
        var act = async () => await handler.Handle(
            new AppointmentCreatedDomainEvent(appointment.Id, appointment.AgentId, appointment.StartDateTime),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(AppointmentType.Service)]
    [InlineData(AppointmentType.Delivery)]
    public async Task Handle_ShouldSkip_WhenAppointmentTypeIsNotTestDrive(AppointmentType type)
    {
        var context = CreateContext();
        var lead = SeedLead(context, LeadStatus.Nuevo);
        var appointment = SeedAppointment(context, lead.Id, null, type);

        var handler = CreateHandler(context);
        await handler.Handle(new AppointmentCreatedDomainEvent(appointment.Id, appointment.AgentId, appointment.StartDateTime), CancellationToken.None);

        var updatedLead = await context.Leads.FirstAsync(l => l.Id == lead.Id);
        updatedLead.Status.Should().Be(LeadStatus.Nuevo);
    }

    [Theory]
    [InlineData(LeadStatus.Negociacion)]
    [InlineData(LeadStatus.Ganado)]
    [InlineData(LeadStatus.Perdido)]
    public async Task Handle_ShouldNotRegressOrTouch_WhenLeadAlreadyAtOrPastDemostracionOrTerminal(LeadStatus initialStatus)
    {
        var context = CreateContext();
        var lead = SeedLead(context, initialStatus);
        var appointment = SeedAppointment(context, lead.Id, null, AppointmentType.TestDrive);

        var handler = CreateHandler(context);
        await handler.Handle(new AppointmentCreatedDomainEvent(appointment.Id, appointment.AgentId, appointment.StartDateTime), CancellationToken.None);

        var updatedLead = await context.Leads.IgnoreQueryFilters().FirstAsync(l => l.Id == lead.Id);
        updatedLead.Status.Should().Be(initialStatus);
    }

    [Fact]
    public async Task Handle_ShouldNoOp_WhenAppointmentDoesNotExist()
    {
        var context = CreateContext();
        var handler = CreateHandler(context);

        var act = async () => await handler.Handle(
            new AppointmentCreatedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
