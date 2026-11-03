namespace Application.DealerSettings.Commands.SetBusinessHours;

/// <summary>Application-layer mirror of <see cref="Domain.DealerSettings.BusinessHoursEntry"/>.</summary>
public sealed record BusinessHoursEntryDto(
    DayOfWeek DayOfWeek,
    TimeOnly? OpenTime,
    TimeOnly? CloseTime);
