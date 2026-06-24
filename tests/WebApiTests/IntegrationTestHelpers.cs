using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System;

namespace WebApiTests;

/// <summary>
/// Helpers para tests de integraciÃ³n
/// </summary>
public static class IntegrationTestHelpers
{
    public static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public static void SetAuthToken(System.Net.Http.HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public static async Task<string> GetAdminTokenAsync(CustomWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var loginRequest = new
        {
            Email = "admin@carstore.com",
            Password = "Admin123!"
        };

        var loginResponse = await client.PostAsJsonAsync("/api/v1/users/login", loginRequest, JsonOptions);
        loginResponse.EnsureSuccessStatusCode();
        
        var result = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        return result!.Token;
    }

    private sealed record LoginResponse(string Token);
}
