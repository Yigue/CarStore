using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Integrations.Meta;

/// <summary>
/// sfc C1.4 / B13: the Lead Ads webhook notification carries only <c>leadgen_id</c> + <c>page_id</c> + <c>form_id</c>
/// — never the submitted answers. The actual field data (name/email/phone, whichever the form asked for) and the
/// platform ("fb" or "ig") are fetched with this follow-up Graph API call, using the connected page's access token.
/// </summary>
public sealed record MetaLeadFieldData(
    string? Platform,
    string? AdId,
    string? AdsetId,
    string? CampaignId,
    string? CampaignName,
    string? FormId,
    string? FullName,
    string? Email,
    string? Phone);

public interface IMetaGraphClient
{
    Task<MetaLeadFieldData?> GetLeadAsync(string leadgenId, string pageAccessToken, CancellationToken cancellationToken);
}

/// <summary>Real Graph API client. Tests use a fake <see cref="IMetaGraphClient"/> — no live call is ever made in this suite.</summary>
public sealed class MetaGraphClient : IMetaGraphClient
{
    private const string GraphVersion = "v19.0";
    private const string Fields = "id,created_time,ad_id,adset_id,campaign_id,campaign_name,form_id,platform,field_data";

    private readonly HttpClient _httpClient;

    public MetaGraphClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<MetaLeadFieldData?> GetLeadAsync(string leadgenId, string pageAccessToken, CancellationToken cancellationToken)
    {
        string url = $"https://graph.facebook.com/{GraphVersion}/{Uri.EscapeDataString(leadgenId)}" +
                     $"?fields={Fields}&access_token={Uri.EscapeDataString(pageAccessToken)}";

        using HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseLead(json);
    }

    /// <summary>Parses a Graph API <c>/{leadgen_id}</c> response. Internal so tests can exercise field_data mapping directly.</summary>
    internal static MetaLeadFieldData ParseLead(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        string? fullName = null;
        string? email = null;
        string? phone = null;

        if (root.TryGetProperty("field_data", out JsonElement fieldData) && fieldData.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement field in fieldData.EnumerateArray())
            {
                string name = field.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty;
                string? value = null;
                if (field.TryGetProperty("values", out JsonElement values) && values.ValueKind == JsonValueKind.Array && values.GetArrayLength() > 0)
                {
                    value = values[0].GetString();
                }

                switch (name.ToLowerInvariant())
                {
                    case "full_name":
                    case "name":
                        fullName = value;
                        break;
                    case "email":
                        email = value;
                        break;
                    case "phone_number":
                    case "phone":
                        phone = value;
                        break;
                }
            }
        }

        return new MetaLeadFieldData(
            GetString(root, "platform"),
            GetString(root, "ad_id"),
            GetString(root, "adset_id"),
            GetString(root, "campaign_id"),
            GetString(root, "campaign_name"),
            GetString(root, "form_id"),
            fullName,
            email,
            phone);
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
