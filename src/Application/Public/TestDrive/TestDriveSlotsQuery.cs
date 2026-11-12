using Application.Abstractions.Messaging;

namespace Application.Public.TestDrive;

/// <summary>
/// sfc B5.2.1 / RN-044: public, anonymous — available test-drive slots for a vehicle on a given
/// day. <c>Date</c> is a wall-clock Argentina calendar date (see
/// <see cref="Domain.DealerSettings.DealerSettings.BusinessTimeZoneId"/>'s locked-decision
/// comment); there is no timezone conversion anywhere in this flow.
/// </summary>
public sealed record TestDriveSlotsQuery(Guid CarId, DateOnly Date) : IQuery<IReadOnlyList<TestDriveSlotResponse>>;

public sealed record TestDriveSlotResponse(DateTime Start, DateTime End);
