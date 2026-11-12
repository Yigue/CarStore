namespace Domain.DealerSettings;

/// <summary>
/// sfc B5.2 / RN-044: one weekday's opening window for the public test-drive scheduler.
///
/// <para>
/// Persisted as a JSON array on <see cref="DealerSettings.BusinessHoursJson"/> — same convention as
/// <see cref="CarouselSlide"/> (a small ordered/keyed list a single admin edits as a whole, never
/// queried or joined against). A day absent from the array means the dealer is closed that day;
/// <see cref="OpenTime"/>/<see cref="CloseTime"/> both <c>null</c> means the same thing for a day
/// that IS present (e.g. an explicit "closed" row from the settings form).
/// </para>
/// </summary>
public sealed record BusinessHoursEntry(
    System.DayOfWeek DayOfWeek,
    System.TimeOnly? OpenTime,
    System.TimeOnly? CloseTime);
