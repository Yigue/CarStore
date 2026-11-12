using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Appointments;
using Domain.Cars;
using Domain.DealerSettings;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Public.TestDrive;

/// <summary>
/// sfc B5.2.1 / RN-044: slots = the car's dealer <see cref="DealerSettings.GetBusinessHours"/> for
/// the requested day, sliced into <see cref="TestDriveScheduling.SlotDurationMinutes"/>-long
/// windows, minus any window where the vehicle already has a non-cancelled/no-show appointment
/// (<see cref="AppointmentConflictQuery.HasConflictAsync"/> — called with <c>agentId: null</c>,
/// so (per that query's rule) only the vehicle side of the conflict check applies, matching a
/// <c>Requested</c> appointment having no agent yet).
///
/// No business hours configured for the dealer, or none for that specific day of week (including
/// an explicit "closed" entry with both times null) → empty list. This is the normal state for a
/// dealer that has not configured <see cref="DealerSettings.GetBusinessHours"/> yet; it must not
/// 500, and it must not invent a default schedule.
/// </summary>
internal sealed class TestDriveSlotsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<TestDriveSlotsQuery, IReadOnlyList<TestDriveSlotResponse>>
{
    public async Task<Result<IReadOnlyList<TestDriveSlotResponse>>> Handle(
        TestDriveSlotsQuery query,
        CancellationToken cancellationToken)
    {
        Car? car = await context.Cars
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == query.CarId, cancellationToken);

        if (car is null)
        {
            return Result.Failure<IReadOnlyList<TestDriveSlotResponse>>(TestDriveErrors.CarNotFound(query.CarId));
        }

        Domain.DealerSettings.DealerSettings? dealerSettings = await context.DealerSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(ds => ds.DealerId == car.DealerId, cancellationToken);

        if (dealerSettings is null)
        {
            return Result.Success<IReadOnlyList<TestDriveSlotResponse>>([]);
        }

        BusinessHoursEntry? entry = dealerSettings.GetBusinessHours()
            .FirstOrDefault(e => e.DayOfWeek == query.Date.DayOfWeek);

        if (entry is null || entry.OpenTime is null || entry.CloseTime is null)
        {
            return Result.Success<IReadOnlyList<TestDriveSlotResponse>>([]);
        }

        var candidates = new List<(DateTime Start, DateTime End)>();
        TimeOnly slotStart = entry.OpenTime.Value;
        while (slotStart.AddMinutes(TestDriveScheduling.SlotDurationMinutes) <= entry.CloseTime.Value)
        {
            TimeOnly slotEnd = slotStart.AddMinutes(TestDriveScheduling.SlotDurationMinutes);
            candidates.Add((query.Date.ToDateTime(slotStart), query.Date.ToDateTime(slotEnd)));
            slotStart = slotEnd;
        }

        var freeSlots = new List<TestDriveSlotResponse>();
        foreach ((DateTime start, DateTime end) in candidates)
        {
            bool hasConflict = await context.Appointments.HasConflictAsync(
                car.DealerId,
                car.Id,
                agentId: null,
                start,
                end,
                excludeAppointmentId: null,
                cancellationToken);

            if (!hasConflict)
            {
                freeSlots.Add(new TestDriveSlotResponse(start, end));
            }
        }

        return Result.Success<IReadOnlyList<TestDriveSlotResponse>>(freeSlots);
    }
}
