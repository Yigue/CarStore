using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.Appointments.Commands.DeleteAppointment;
using Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Appointments;

public class DeleteAppointmentCommandHandlerTests
{
    private static readonly Guid TestDealerId = Guid.Parse("aaaabbbb-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherDealerId = Guid.Parse("cccceeee-cccc-cccc-cccc-cccccccccccc");
    private static readonly DateTime Now = new(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc);

    private static (TestApplicationDbContext context, ICurrentTenantService tenantService) CreateSut()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new TestApplicationDbContext(options);

        var mockTenant = new Mock<ICurrentTenantService>();
        mockTenant.Setup(t => t.DealerId).Returns(TestDealerId);
        mockTenant.Setup(t => t.HasTenant).Returns(true);

        return (context, mockTenant.Object);
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
    public async Task Handle_DeletesAppointment_WhenWithinTenant()
    {
        var (context, tenantService) = CreateSut();
        using (context)
        {
            Appointment appointment = SeedAppointment(context, TestDealerId);
            var handler = new DeleteAppointmentCommandHandler(context, tenantService);

            var result = await handler.Handle(new DeleteAppointmentCommand(appointment.Id), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            (await context.Appointments.FindAsync(appointment.Id)).Should().BeNull();
        }
    }

    [Fact]
    public async Task Handle_Fails_WhenAppointmentBelongsToAnotherDealer()
    {
        var (context, tenantService) = CreateSut();
        using (context)
        {
            // Seeded under a different dealer than the one the tenant service resolves —
            // TestApplicationDbContext has no global query filter, so only the handler's
            // own DealerId check can stop a cross-tenant delete.
            Appointment appointment = SeedAppointment(context, OtherDealerId);
            var handler = new DeleteAppointmentCommandHandler(context, tenantService);

            var result = await handler.Handle(new DeleteAppointmentCommand(appointment.Id), CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
            result.Error.Code.Should().Be("Appointments.NotFound");
            (await context.Appointments.FindAsync(appointment.Id)).Should().NotBeNull("a cross-tenant delete attempt must not remove another dealer's row");
        }
    }

    [Fact]
    public async Task Handle_Fails_WhenAppointmentDoesNotExist()
    {
        var (context, tenantService) = CreateSut();
        using (context)
        {
            var handler = new DeleteAppointmentCommandHandler(context, tenantService);

            var result = await handler.Handle(new DeleteAppointmentCommand(Guid.NewGuid()), CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
            result.Error.Code.Should().Be("Appointments.NotFound");
        }
    }
}
