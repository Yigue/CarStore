using System.Text.Json;
using System.Text.Json.Serialization;

namespace Web.Api.Infrastructure;

/// <summary>
/// Normalizes every inbound <see cref="DateTime"/> to <see cref="DateTimeKind.Utc"/>.
/// PostgreSQL <c>timestamp with time zone</c> columns (via Npgsql) reject
/// <see cref="DateTimeKind.Unspecified"/> values, and clients commonly send wall-clock
/// strings without an offset (e.g. <c>2026-06-24T00:00:00</c>). Registering this converter
/// globally guarantees a single, consistent UTC boundary for the whole API instead of
/// per-handler patches. <see cref="DateTimeKind.Local"/> values are converted to UTC;
/// <see cref="DateTimeKind.Unspecified"/> values are interpreted as UTC.
/// Nullable <c>DateTime?</c> is handled automatically by System.Text.Json's Nullable wrapper.
/// </summary>
internal sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        DateTime value = reader.GetDateTime();

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        DateTime utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        writer.WriteStringValue(utc);
    }
}
