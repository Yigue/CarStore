namespace Application.Common;

/// <summary>
/// Upper bounds for list endpoints. A caller asking for <c>pageSize=100000</c> or a ten-year
/// report window gets the maximum instead of making the server materialise all of it.
/// </summary>
public static class Pagination
{
    public const int MaxPageSize = 100;
    public const int MaxDateRangeDays = 366;

    public static int ClampPageSize(int requested) => Math.Clamp(requested, 1, MaxPageSize);

    public static int ClampPage(int requested) => Math.Max(requested, 1);

    /// <summary>Keeps <paramref name="from"/> and cuts <paramref name="to"/> to at most <see cref="MaxDateRangeDays"/> later.</summary>
    public static (DateTime From, DateTime To) ClampRange(DateTime from, DateTime to)
    {
        DateTime limit = from.AddDays(MaxDateRangeDays);
        return (from, to > limit ? limit : to);
    }
}
