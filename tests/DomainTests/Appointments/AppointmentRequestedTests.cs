using Domain.Appointments;
using Domain.Appointments.Events;

// sfc B5.2.3 / H15: a requested appointment has no agent until someone confirms it.
public class AppointmentRequestedTests
{
    private static readonly Guid DealerId = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly Guid LeadId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc);

    private static Appointment NewRequested() => Appointment.Request(
        DealerId, VehicleId, clientId: null, leadId: LeadId,
        Now.AddDays(1), Now.AddDays(1).AddHours(1), AppointmentType.TestDrive, "web", Now);

    [Fact]
    public void Request_CreatesRequestedAppointmentWithoutAgent()
    {
        Appointment a = NewRequested();

        a.Status.Should().Be(AppointmentStatus.Requested);
        a.AgentId.Should().BeNull();
        a.LeadId.Should().Be(LeadId);
        a.DealerId.Should().Be(DealerId);
    }

    [Fact]
    public void Request_RaisesCreatedEventWithNullAgent()
    {
        Appointment a = NewRequested();

        a.DomainEvents.OfType<AppointmentCreatedDomainEvent>().Single().AgentId.Should().BeNull();
    }

    [Fact]
    public void Request_Fails_WithoutClientOrLead()
    {
        Action act = () => Appointment.Request(
            DealerId, VehicleId, clientId: null, leadId: null,
            Now.AddDays(1), Now.AddDays(1).AddHours(1), AppointmentType.TestDrive, null, Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Confirm_AssignsAgentAndSchedules()
    {
        Appointment a = NewRequested();
        Guid agentId = Guid.NewGuid();

        a.Confirm(agentId);

        a.Status.Should().Be(AppointmentStatus.Scheduled);
        a.AgentId.Should().Be(agentId);
    }

    [Fact]
    public void Confirm_Twice_Fails()
    {
        Appointment a = NewRequested();
        a.Confirm(Guid.NewGuid());

        Action act = () => a.Confirm(Guid.NewGuid());

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Confirm_WithEmptyAgent_Fails()
    {
        Appointment a = NewRequested();

        Action act = () => a.Confirm(Guid.Empty);

        act.Should().Throw<DomainException>();
        a.Status.Should().Be(AppointmentStatus.Requested);
    }

    [Fact]
    public void Cancel_FromRequested_Succeeds()
    {
        Appointment a = NewRequested();

        a.Cancel();

        a.Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact]
    public void Reschedule_FromRequested_Fails()
    {
        Appointment a = NewRequested();

        Action act = () => a.Reschedule(Now.AddDays(2), Now.AddDays(2).AddHours(1), Now);

        act.Should().Throw<DomainException>();
    }
}
