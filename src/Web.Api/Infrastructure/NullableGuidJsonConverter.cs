using System.Text.Json;
using System.Text.Json.Serialization;

namespace Web.Api.Infrastructure;

/// <summary>
/// Binds optional <see cref="Guid"/> request fields defensively. Browsers and form libraries
/// frequently serialize "no selection" as an empty string (<c>""</c>) rather than omitting the
/// property or sending <c>null</c>. System.Text.Json cannot parse <c>""</c> into a
/// <see cref="Guid"/>, which surfaced as a 500 (<c>BadHttpRequestException</c>) before the handler
/// ran. Registering this converter globally maps empty/whitespace strings to <c>null</c> while
/// preserving strict parsing for real values, giving the whole API a single, consistent boundary
/// instead of per-endpoint patches.
/// </summary>
internal sealed class NullableGuidJsonConverter : JsonConverter<Guid?>
{
    public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            string? value = reader.GetString();

            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return Guid.Parse(value);
        }

        return reader.GetGuid();
    }

    public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
