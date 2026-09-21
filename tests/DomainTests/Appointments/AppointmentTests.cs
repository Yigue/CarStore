using Domain.Appointments;
using Domain.Appointments.Events;

public class AppointmentTests
{
    private static readonly Guid DealerId = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly Guid AgentId = Guid.NewGuid();

    private static Appointment CreateValidAppointment(DateTime? createdAtUtc = null)
    {
        DateTime now = createdAtUtc ?? DateTime.UtcNow;
        return Appointment.Create(
            DealerId,
            VehicleId,
            ClientId,
            leadId: null,
            AgentId,
            start: now.AddDays(1),
            end: now.AddDays(1).AddHours(1),
            AppointmentType.TestDrive,
            notes: "Test drive inicial",
            createdAtUtc: now);
    }

    [Fact]
    public void Create_ShouldInitializePropertiesAndRaiseEvent()
    {
        DateTime now = DateTime.UtcNow;
        DateTime start = now.AddDays(1);
        DateTime end = start.AddHours(1);

        var appointment = Appointment.Create(
            DealerId, VehicleId, ClientId, leadId: null, AgentId,
            start, end, AppointmentType.Service, "Cambio de aceite", now);

        appointment.DealerId.Should().Be(DealerId);
        appointment.VehicleId.Should().Be(VehicleId);
        appointment.ClientId.Should().Be(ClientId);
        appointment.LeadId.Should().BeNull();
        appointment.AgentId.Should().Be(AgentId);
        appointment.StartDateTime.Should().Be(start);
        appointment.EndDateTime.Should().Be(end);
        appointment.Type.Should().Be(AppointmentType.Service);
        appointment.Status.Should().Be(AppointmentStatus.Scheduled);
        appointment.Notes.Should().Be("Cambio de aceite");

        var domainEvent = appointment.DomainEvents.Single().Should().BeOfType<AppointmentCreatedDomainEvent>().Subject;
        domainEvent.AppointmentId.Should().Be(appointment.Id);
        domainEvent.AgentId.Should().Be(AgentId);
        domainEvent.Start.Should().Be(start);
    }

    [Fact]
    public void Create_Throws_WhenVehicleIdIsEmpty()
    {
        DateTime now = DateTime.UtcNow;
        Action act = () => Appointment.Create(
            DealerId, Guid.Empty, ClientId, leadId: null, AgentId,
            now.AddDays(1), now.AddDays(1).AddHours(1), AppointmentType.TestDrive, null, now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_Throws_WhenNeitherClientNorLeadIsProvided()
    {
        DateTime now = DateTime.UtcNow;
        Action act = () => Appointment.Create(
            DealerId, VehicleId, clientId: null, leadId: null, AgentId,
            now.AddDays(1), now.AddDays(1).AddHours(1), AppointmentType.TestDrive, null, now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_Throws_WhenAgentIdIsEmpty()
    {
        DateTime now = DateTime.UtcNow;
        Action act = () => Appointment.Create(
            DealerId, VehicleId, ClientId, leadId: null, Guid.Empty,
            now.AddDays(1), now.AddDays(1).AddHours(1), AppointmentType.TestDrive, null, now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_Throws_WhenEndIsNotAfterStart()
    {
        DateTime now = DateTime.UtcNow;
        DateTime start = now.AddDays(1);

        Action act = () => Appointment.Create(
            DealerId, VehicleId, ClientId, leadId: null, AgentId,
            start, start, AppointmentType.TestDrive, null, now);

        act.Should().Throw<DomainException>()
            .WithMessage("La hora de fin debe ser posterior a la hora de inicio");
    }

    [Fact]
    public void Create_Throws_WhenStartIsOnAPastDate()
    {
        DateTime now = DateTime.UtcNow;
        DateTime pastStart = now.AddDays(-1);

        Action act = () => Appointment.Create(
            DealerId, VehicleId, ClientId, leadId: null, AgentId,
            pastStart, pastStart.AddHours(1), AppointmentType.TestDrive, null, now);

        act.Should().Throw<DomainException>()
            .WithMessage("No se puede agendar un turno en una fecha pasada");
    }

    [Fact]
    public void Create_Allows_StartOnTheSameCalendarDayAsCreation()
    {
        // A walk-in booked "right now" must not be rejected as "in the past" —
        // the guard is date-level, not a strict instant-level comparison.
        DateTime now = DateTime.UtcNow;

        Action act = () => Appointment.Create(
            DealerId, VehicleId, ClientId, leadId: null, AgentId,
            now, now.AddMinutes(30), AppointmentType.TestDrive, null, now);

        act.Should().NotThrow();
    }

    [Fact]
    public void Reschedule_UpdatesTimesAndRaisesEvent_WhenAppointmentIsScheduled()
    {
        Appointment appointment = CreateValidAppointment();
        appointment.ClearDomainEvents();
        DateTime now = DateTime.UtcNow;
        DateTime newStart = now.AddDays(2);
        DateTime newEnd = newStart.AddHours(1);

        appointment.Reschedule(newStart, newEnd, now);

        appointment.StartDateTime.Should().Be(newStart);
        appointment.EndDateTime.Should().Be(newEnd);
        appointment.DomainEvents.Single().Should().BeOfType<AppointmentRescheduledDomainEvent>();
    }

    [Fact]
    public void Reschedule_Throws_WhenEndIsNotAfterNewStart()
    {
        Appointment appointment = CreateValidAppointment();
        DateTime now = DateTime.UtcNow;
        DateTime newStart = now.AddDays(2);

        Action act = () => appointment.Reschedule(newStart, newStart, now);

        act.Should().Throw<DomainException>()
            .WithMessage("La hora de fin debe ser posterior a la hora de inicio");
    }

    [Fact]
    public void Reschedule_Throws_WhenNewStartIsOnAPastDate()
    {
        Appointment appointment = CreateValidAppointment();
        DateTime now = DateTime.UtcNow;
        DateTime pastStart = now.AddDays(-1);

        Action act = () => appointment.Reschedule(pastStart, pastStart.AddHours(1), now);

        act.Should().Throw<DomainException>()
            .WithMessage("No se puede reagendar un turno a una fecha pasada");
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void Reschedule_Throws_WhenAppointmentIsNoLongerScheduled(AppointmentStatus closedStatus)
    {
        Appointment appointment = CreateValidAppointment();
        MoveToStatus(appointment, closedStatus);
        DateTime now = DateTime.UtcNow;

        Action act = () => appointment.Reschedule(now.AddDays(3), now.AddDays(3).AddHours(1), now);

        act.Should().Throw<DomainException>()
            .WithMessage($"No se puede modificar un turno en estado {closedStatus}");
    }

    [Fact]
    public void Complete_SetsStatusToCompleted_WhenScheduled()
    {
        Appointment appointment = CreateValidAppointment();

        appointment.Complete();

        appointment.Status.Should().Be(AppointmentStatus.Completed);
    }

    [Fact]
    public void Cancel_SetsStatusToCancelled_WhenScheduled()
    {
        Appointment appointment = CreateValidAppointment();

        appointment.Cancel();

        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact]
    public void MarkNoShow_SetsStatusToNoShow_WhenScheduled()
    {
        Appointment appointment = CreateValidAppointment();

        appointment.MarkNoShow();

        appointment.Status.Should().Be(AppointmentStatus.NoShow);
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void Complete_Throws_WhenAppointmentIsAlreadyClosed(AppointmentStatus closedStatus)
    {
        Appointment appointment = CreateValidAppointment();
        MoveToStatus(appointment, closedStatus);

        Action act = () => appointment.Complete();

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void Cancel_Throws_WhenAppointmentIsAlreadyClosed(AppointmentStatus closedStatus)
    {
        Appointment appointment = CreateValidAppointment();
        MoveToStatus(appointment, closedStatus);

        Action act = () => appointment.Cancel();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AssignClient_UpdatesClientAndClearsLead()
    {
        DateTime now = DateTime.UtcNow;
        Guid leadId = Guid.NewGuid();
        Appointment appointment = Appointment.Create(
            DealerId, VehicleId, clientId: null, leadId, AgentId,
            now.AddDays(1), now.AddDays(1).AddHours(1), AppointmentType.TestDrive, null, now);

        Guid newClientId = Guid.NewGuid();
        appointment.AssignClient(newClientId);

        appointment.ClientId.Should().Be(newClientId);
        appointment.LeadId.Should().BeNull();
    }

    [Fact]
    public void AssignClient_Throws_WhenClientIdIsEmpty()
    {
        Appointment appointment = CreateValidAppointment();

        Action act = () => appointment.AssignClient(Guid.Empty);

        act.Should().Throw<DomainException>();
    }

    private static void MoveToStatus(Appointment appointment, AppointmentStatus status)
    {
        switch (status)
        {
            case AppointmentStatus.Completed:
                appointment.Complete();
                break;
            case AppointmentStatus.Cancelled:
                appointment.Cancel();
                break;
            case AppointmentStatus.NoShow:
                appointment.MarkNoShow();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }
    }
}
