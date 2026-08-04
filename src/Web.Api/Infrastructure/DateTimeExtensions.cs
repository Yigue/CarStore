using System;

namespace Web.Api.Infrastructure;

public static class DateTimeExtensions
{
    /// <summary>
    /// Normalizes a <see cref="DateTime"/> instance to UTC.
    /// <para>
    /// Unspecified kind is treated as UTC midnight/time (per design D6 convention for query params).
    /// Local kind is converted to UTC via <see cref="DateTime.ToUniversalTime"/>.
    /// Utc kind is returned unchanged.
    /// </para>
    /// </summary>
    public static DateTime ToUtc(this DateTime dateTime)
    {
        return dateTime.Kind switch
        {
            DateTimeKind.Unspecified => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            _ => dateTime
        };
    }
}
