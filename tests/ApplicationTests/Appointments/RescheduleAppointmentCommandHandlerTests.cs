using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.Appointments.Commands.RescheduleAppointment;
using Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Appointments;

public class RescheduleAppointmentCommandHandlerTests
{
    private static readonly Guid TestDealerId = Guid.Parse("aaaabbbb-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherDealerId = Guid.Parse("cccceeee-cccc-cccc-cccc-cccccccccccc");
    private static readonly DateTime Now = new(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc);

    private static (TestApplicationDbContext context, ICurrentTenantService tenantService, FakeDateTimeProvider dateProvider) CreateSut()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new TestApplicationDbContext(options);

        var mockTenant = new Mock<ICurrentTenantService>();
        mockTenant.Setup(t => t.DealerId).Returns(TestDealerId);
        mockTenant.Setup(t => t.HasTenant).Returns(true);

        var dateProvider = new FakeDateTimeProvider { UtcNow = Now };

        return (context, mockTenant.Object, dateProvider);
    }

    private static Appointment SeedAppointment(TestApplicationDbContext context, Guid dealerId)
    {
        var appointment = Appointment.Create(
            dealerId, Guid.NewGuid(), Guid.NewGuid(), leadId: null, Guid.NewGuid(),
            Now.AddDays(1), Now.AddDays(1).AddHours(1), AppointmentType.TestDrive, null, Now);
        appointment.ClearDomainEvents();
        context.Appointments.Add(appointment);
        context.SaveChanges();
        return appointment;
    }

    [Fact]
    public async Task Handle_ReschedulesAppointment_WhenScheduledAndWithinTenant()
    {
        var (context, tenantService, dateProvider) = CreateSut();
        using (context)
        {
            Appointment appointment = SeedAppointment(context, TestDealerId);
            var handler = new RescheduleAppointmentCommandHandler(context, tenantService, dateProvider);
            var newStart = Now.AddDays(2);
            var command = new RescheduleAppointmentCommand(appointment.Id, newStart, newStart.AddHours(1));

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            var persisted = await context.Appointments.FindAsync(appointment.Id);
            persisted!.StartDateTime.Should().Be(newStart);
        }
    }

    [Fact]
    public async Task Handle_Fails_WhenAppointmentBelongsToAnotherDealer()
    {
        var (context, tenantService, dateProvider) = CreateSut();
        using (context)
        {
            // Seeded under a different dealer than the one the tenant service resolves —
            // TestApplicationDbContext has no global query filter, so only the handler's
            // own DealerId check can stop a cross-tenant reschedule.
            Appointment appointment = SeedAppointment(context, OtherDealerId);
            var handler = new RescheduleAppointmentCommandHandler(context, tenantService, dateProvider);
            var newStart = Now.AddDays(2);
            var command = new RescheduleAppointmentCommand(appointment.Id, newStart, newStart.AddHours(1));

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
            result.Error.Code.Should().Be("Appointments.NotFound");
            var persisted = await context.Appointments.FindAsync(appointment.Id);
            persisted!.StartDateTime.Should().Be(Now.AddDays(1), "a cross-tenant reschedule attempt must not mutate the row");
        }
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public async Task Handle_Fails_WhenAppointmentIsNoLongerScheduled(AppointmentStatus closedStatus)
    {
        var (context, tenantService, dateProvider) = CreateSut();
        using (context)
        {
            Appointment appointment = SeedAppointment(context, TestDealerId);
            switch (closedStatus)
            {
                case AppointmentStatus.Completed: appointment.Complete(); break;
                case AppointmentStatus.Cancelled: appointment.Cancel(); break;
                case AppointmentStatus.NoShow: appointment.MarkNoShow(); break;
            }
            await context.SaveChangesAsync();

            var handler = new RescheduleAppointmentCommandHandler(context, tenantService, dateProvider);
            var newStart = Now.AddDays(2);
            var command = new RescheduleAppointmentCommand(appointment.Id, newStart, newStart.AddHours(1));

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
            result.Error.Code.Should().Be("Appointment.DomainError");
        }
    }

    [Fact]
    public async Task Handle_Fails_WhenConflictingWithAnotherAppointmentForTheSameAgent()
    {
        var (context, tenantService, dateProvider) = CreateSut();
        using (context)
        {
            Appointment moving = SeedAppointment(context, TestDealerId);
            var blockingStart = Now.AddDays(2);
            var blocking = Appointment.Create(
                TestDealerId, Guid.NewGuid(), Guid.NewGuid(), leadId: null, moving.AgentId,
                blockingStart, blockingStart.AddHours(1), AppointmentType.Service, null, Now);
            blocking.ClearDomainEvents();
            context.Appointments.Add(blocking);
            await context.SaveChangesAsync();

            var handler = new RescheduleAppointmentCommandHandler(context, tenantService, dateProvider);
            var command = new RescheduleAppointmentCommand(moving.Id, blockingStart, blockingStart.AddHours(1));

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
            result.Error.Code.Should().Be("Appointments.Conflict");
        }
    }
}
