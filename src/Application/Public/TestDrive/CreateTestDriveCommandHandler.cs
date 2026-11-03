using Application.Abstractions;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Appointments;
using Domain.Appointments;
using Domain.Cars;
using Domain.DealerSettings;
using Domain.Leads;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Public.TestDrive;

/// <summary>
/// sfc B5.2.2/B5.2.4 / RN-044: creates a <c>Requested</c> (no agent yet) test-drive appointment
/// from the public catalog. Reuses <c>CreateInquiryCommandHandler</c>'s find-or-append Lead
/// pattern — a visitor booking twice is one prospect, not two — and
/// <c>CreateAppointmentCommandHandler</c>'s rule that a test-drive appointment on an open lead
/// advances it to <see cref="LeadStatus.Demostracion"/> (sfc B5.2.3's note: parity between the
/// dashboard-created and publicly-requested paths).
/// </summary>
internal sealed class CreateTestDriveCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateTestDriveCommand, Guid>
{
    /// <summary>Same closed-lead posture as <c>CreateInquiryCommandHandler</c>: a fresh request deserves a fresh lead.</summary>
    private static readonly LeadStatus[] ClosedStatuses =
        [LeadStatus.Ganado, LeadStatus.Perdido, LeadStatus.Archivado];

    /// <summary>RN-044: at most this many simultaneous Requested test drives per contact per dealer.</summary>
    private const int MaxSimultaneousRequested = 2;

    public async Task<Result<Guid>> Handle(CreateTestDriveCommand command, CancellationToken cancellationToken)
    {
        // Honeypot (B5.2.2): a field no human ever fills in. A bot that fills every field gets
        // the exact same success shape a real submission would — it must not learn it was
        // detected — but nothing is read or written past this point.
        if (!string.IsNullOrWhiteSpace(command.Website))
        {
            return Result.Success(Guid.NewGuid());
        }

        Car? car = await context.Cars.SingleOrDefaultAsync(c => c.Id == command.CarId, cancellationToken);
        if (car is null)
        {
            return Result.Failure<Guid>(TestDriveErrors.CarNotFound(command.CarId));
        }

        Guid dealerId = car.DealerId;

        Email email;
        try
        {
            email = new Email(command.Email);
        }
        catch (DomainException ex)
        {
            return Result.Failure<Guid>(Error.Validation("Email.Invalid", ex.Message));
        }

        DateTime start = command.Start;
        DateTime end = start.AddMinutes(TestDriveScheduling.SlotDurationMinutes);

        Domain.DealerSettings.DealerSettings? dealerSettings = await context.DealerSettings
            .FirstOrDefaultAsync(ds => ds.DealerId == dealerId, cancellationToken);

        BusinessHoursEntry? entry = dealerSettings?.GetBusinessHours()
            .FirstOrDefault(e => e.DayOfWeek == start.DayOfWeek);

        if (entry is null || entry.OpenTime is null || entry.CloseTime is null)
        {
            return Result.Failure<Guid>(TestDriveErrors.OutsideBusinessHours);
        }

        TimeOnly startTime = TimeOnly.FromDateTime(start);
        TimeOnly endTime = TimeOnly.FromDateTime(end);
        if (startTime < entry.OpenTime.Value || endTime > entry.CloseTime.Value)
        {
            return Result.Failure<Guid>(TestDriveErrors.OutsideBusinessHours);
        }

        bool hasConflict = await context.Appointments.HasConflictAsync(
            dealerId,
            car.Id,
            agentId: null,
            start,
            end,
            excludeAppointmentId: null,
            cancellationToken);

        if (hasConflict)
        {
            return Result.Failure<Guid>(TestDriveErrors.Conflict);
        }

        // RN-044: a third simultaneous Requested booking from the same contact (email or phone)
        // for this dealer is throttled — join through the Lead because the appointment itself
        // carries no email/phone, only a LeadId.
        int requestedCount = await context.Appointments
            .Where(a => a.DealerId == dealerId && a.Status == AppointmentStatus.Requested && a.LeadId != null)
            .Join(context.Leads, a => a.LeadId, l => l.Id, (a, l) => l)
            .CountAsync(l => l.Email == email || l.Phone == command.Phone, cancellationToken);

        if (requestedCount >= MaxSimultaneousRequested)
        {
            return Result.Failure<Guid>(TestDriveErrors.TooManyRequested);
        }

        Lead lead = await FindOrCreateLeadAsync(dealerId, email, command, car, cancellationToken);

        Appointment appointment;
        try
        {
            appointment = Appointment.Request(
                dealerId,
                car.Id,
                clientId: null,
                lead.Id,
                start,
                end,
                AppointmentType.TestDrive,
                command.Comments,
                dateTimeProvider.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result.Failure<Guid>(Error.Validation("TestDrive.DomainError", ex.Message));
        }

        context.Appointments.Add(appointment);

        // Parity with CreateAppointmentCommandHandler (sfc B5.2.3's note): a test-drive
        // appointment on a fresh/contacted lead advances it to Demostración either way.
        if (lead.Status is LeadStatus.Nuevo or LeadStatus.Contactado)
        {
            lead.ForceStatus(LeadStatus.Demostracion);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(appointment.Id);
    }

    private async Task<Lead> FindOrCreateLeadAsync(
        Guid dealerId,
        Email email,
        CreateTestDriveCommand command,
        Car car,
        CancellationToken cancellationToken)
    {
        Lead? existing = await context.Leads
            .Where(l => l.DealerId == dealerId && l.Email == email && !ClosedStatuses.Contains(l.Status))
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            if (existing.InterestedVehicleId is null)
            {
                existing.LinkVehicle(car.Id);
            }

            return existing;
        }

        var lead = Lead.Create(
            dealerId,
            $"{command.FirstName} {command.LastName}".Trim(),
            command.Email,
            command.Phone,
            LeadSource.Web,
            dateTimeProvider.UtcNow,
            car.Id);

        context.Leads.Add(lead);
        return lead;
    }
}
