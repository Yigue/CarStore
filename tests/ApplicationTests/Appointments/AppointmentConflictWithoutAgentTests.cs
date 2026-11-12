using Application.Abstractions.Tenancy;
using Application.Appointments;
using Application.Appointments.Commands.ConfirmAppointment;
using Application.Appointments.Commands.CreateAppointment;
using Domain.Appointments;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Appointments;

// sfc B5.2.3 / H15: conflict is by vehicle only while there is no agent; cancelled slots are bookable.
public class AppointmentConflictWithoutAgentTests
{
    private static readonly Guid DealerId = Guid.Parse("aaaabbbb-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherDealerId = Guid.Parse("cccceeee-cccc-cccc-cccc-cccccccccccc");
    private static readonly DateTime Now = new(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Start = Now.AddDays(1);
    private static readonly DateTime End = Now.AddDays(1).AddHours(1);

    private readonly TestApplicationDbContext _context;
    private readonly ICurrentTenantService _tenant;
    private readonly FakeDateTimeProvider _clock = new() { UtcNow = Now };

    public AppointmentConflictWithoutAgentTests()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new TestApplicationDbContext(options);
        var tenant = new Mock<ICurrentTenantService>();
        tenant.Setup(t => t.DealerId).Returns(DealerId);
        tenant.Setup(t => t.HasTenant).Returns(true);
        _tenant = tenant.Object;
    }

    private User SeedAgent(Guid dealerId, bool active = true)
    {
        var user = new User(dealerId, $"{Guid.NewGuid():N}@example.com", "Ag", "Ent", "hash", Guid.NewGuid());
        if (!active)
            user.Deactivate();
        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    private Appointment SeedScheduled(Guid vehicleId, Guid agentId, DateTime? start = null, DateTime? end = null)
    {
        var a = Appointment.Create(
            DealerId, vehicleId, Guid.NewGuid(), leadId: null, agentId,
            start ?? Start, end ?? End, AppointmentType.TestDrive, null, Now);
        a.ClearDomainEvents();
        _context.Appointments.Add(a);
        _context.SaveChanges();
        return a;
    }

    private Appointment SeedRequested(Guid vehicleId, DateTime? start = null, DateTime? end = null)
    {
        var a = Appointment.Request(
            DealerId, vehicleId, clientId: null, leadId: Guid.NewGuid(),
            start ?? Start, end ?? End, AppointmentType.TestDrive, null, Now);
        a.ClearDomainEvents();
        _context.Appointments.Add(a);
        _context.SaveChanges();
        return a;
    }

    private Task<bool> HasConflict(Guid vehicleId, Guid? agentId, Guid? excludeId = null) =>
        _context.Appointments.HasConflictAsync(
            DealerId, vehicleId, agentId, Start, End, excludeId, CancellationToken.None);

    private Task<Domain.Appointments.Appointment?> Reload(Guid id) =>
        _context.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);

    [Fact]
    public async Task WithoutAgent_SameVehicleOverlap_Conflicts()
    {
        Guid vehicle = Guid.NewGuid();
        SeedScheduled(vehicle, Guid.NewGuid());

        (await HasConflict(vehicle, agentId: null)).Should().BeTrue();
    }

    [Fact]
    public async Task WithoutAgent_OtherVehicleWithBusyAgentSlot_DoesNotConflict()
    {
        SeedScheduled(Guid.NewGuid(), Guid.NewGuid());

        (await HasConflict(Guid.NewGuid(), agentId: null)).Should().BeFalse();
    }

    [Fact]
    public async Task WithAgent_AgentOverlapOnOtherVehicle_Conflicts()
    {
        Guid agent = Guid.NewGuid();
        SeedScheduled(Guid.NewGuid(), agent);

        (await HasConflict(Guid.NewGuid(), agent)).Should().BeTrue();
    }

    [Fact]
    public async Task RequestedWithoutAgent_DoesNotBlockOtherVehicleForAnAgent()
    {
        SeedRequested(Guid.NewGuid());

        (await HasConflict(Guid.NewGuid(), Guid.NewGuid())).Should().BeFalse();
    }

    [Theory]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public async Task ClosedAppointments_DoNotBlock(AppointmentStatus status)
    {
        Guid vehicle = Guid.NewGuid();
        Guid agent = Guid.NewGuid();
        Appointment a = SeedScheduled(vehicle, agent);
        if (status == AppointmentStatus.Cancelled) a.Cancel(); else a.MarkNoShow();
        _context.SaveChanges();

        (await HasConflict(vehicle, agent)).Should().BeFalse();
    }

    [Fact]
    public async Task Create_OnCancelledSlot_Succeeds()
    {
        Guid vehicle = Guid.NewGuid();
        Guid agent = Guid.NewGuid();
        SeedScheduled(vehicle, agent).Cancel();
        _context.SaveChanges();
        var handler = new CreateAppointmentCommandHandler(_context, _tenant, _clock);

        var result = await handler.Handle(
            new CreateAppointmentCommand(vehicle, Guid.NewGuid(), null, agent, Start, End, AppointmentType.TestDrive, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Create_OnVehicleHeldByRequested_Conflicts()
    {
        Guid vehicle = Guid.NewGuid();
        SeedRequested(vehicle);
        var handler = new CreateAppointmentCommandHandler(_context, _tenant, _clock);

        var result = await handler.Handle(
            new CreateAppointmentCommand(vehicle, Guid.NewGuid(), null, Guid.NewGuid(), Start, End, AppointmentType.TestDrive, null),
            CancellationToken.None);

        result.Error.Should().Be(AppointmentErrors.Conflict);
    }

    [Fact]
    public async Task Confirm_AssignsAgent_WhenFree()
    {
        Appointment requested = SeedRequested(Guid.NewGuid());
        User agent = SeedAgent(DealerId);
        var handler = new ConfirmAppointmentCommandHandler(_context, _tenant);

        var result = await handler.Handle(new ConfirmAppointmentCommand(requested.Id, agent.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Appointment? persisted = await Reload(requested.Id);
        persisted!.Status.Should().Be(AppointmentStatus.Scheduled);
        persisted.AgentId.Should().Be(agent.Id);
    }

    [Fact]
    public async Task Confirm_ReturnsConflict_WhenAgentIsBusy()
    {
        Appointment requested = SeedRequested(Guid.NewGuid());
        User agent = SeedAgent(DealerId);
        SeedScheduled(Guid.NewGuid(), agent.Id);
        var handler = new ConfirmAppointmentCommandHandler(_context, _tenant);

        var result = await handler.Handle(new ConfirmAppointmentCommand(requested.Id, agent.Id), CancellationToken.None);

        result.Error.Should().Be(AppointmentErrors.Conflict);
        (await Reload(requested.Id))!.Status.Should().Be(AppointmentStatus.Requested);
    }

    [Fact]
    public async Task Confirm_Fails_WhenAgentBelongsToAnotherDealer()
    {
        Appointment requested = SeedRequested(Guid.NewGuid());
        User foreign = SeedAgent(OtherDealerId);
        var handler = new ConfirmAppointmentCommandHandler(_context, _tenant);

        var result = await handler.Handle(new ConfirmAppointmentCommand(requested.Id, foreign.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AppointmentErrors.AgentNotFound(foreign.Id).Code);
        (await Reload(requested.Id))!.Status.Should().Be(AppointmentStatus.Requested);
    }

    [Fact]
    public async Task Confirm_Fails_WhenAgentIsInactive()
    {
        Appointment requested = SeedRequested(Guid.NewGuid());
        User inactive = SeedAgent(DealerId, active: false);
        var handler = new ConfirmAppointmentCommandHandler(_context, _tenant);

        var result = await handler.Handle(new ConfirmAppointmentCommand(requested.Id, inactive.Id), CancellationToken.None);

        result.Error.Code.Should().Be("Appointments.AgentNotFound");
    }

    [Fact]
    public async Task Confirm_Fails_WhenAppointmentBelongsToAnotherDealer()
    {
        var other = Appointment.Request(
            OtherDealerId, Guid.NewGuid(), null, Guid.NewGuid(), Start, End, AppointmentType.TestDrive, null, Now);
        _context.Appointments.Add(other);
        _context.SaveChanges();
        User agent = SeedAgent(DealerId);
        var handler = new ConfirmAppointmentCommandHandler(_context, _tenant);

        var result = await handler.Handle(new ConfirmAppointmentCommand(other.Id, agent.Id), CancellationToken.None);

        result.Error.Code.Should().Be("Appointments.NotFound");
    }

    [Fact]
    public async Task Confirm_Fails_WhenNotRequested()
    {
        Appointment scheduled = SeedScheduled(Guid.NewGuid(), Guid.NewGuid());
        User agent = SeedAgent(DealerId);
        var handler = new ConfirmAppointmentCommandHandler(_context, _tenant);

        var result = await handler.Handle(new ConfirmAppointmentCommand(scheduled.Id, agent.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Appointments.InvalidStatusTransition");
    }
}
