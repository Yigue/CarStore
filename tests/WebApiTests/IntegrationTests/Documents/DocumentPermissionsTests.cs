using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Domain.Clients;
using Domain.Documents;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApiTests.IntegrationTests.Documents;

/// <summary>
/// qa-p1-integridad PR7, Slice 14 (D7, REQ: document-upload-lifecycle).
/// <para>
/// MUST NOT run before PR6 (grants documents:read/documents:create at both seed sites) is
/// merged and its Slice 11.5 verification is green — otherwise this permission requirement
/// trades one 403 for another (finding 2). PR6 is committed (0a42f23) and 11.5 is independently
/// re-verified here via <see cref="UploadDocument_ThenDownloadOwnUpload_Returns200Both"/>: the
/// seeded Admin token (which carries PR6's grants) succeeds on both calls.
/// </para>
/// </summary>
public class DocumentPermissionsTests
{
    private const string ValidBase64Content = "aGVsbG8gd29ybGQ="; // "hello world"

    private static async Task<string> RegisterAndLoginFreshUserAsync(CustomWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"fresh_{Guid.NewGuid():N}@example.com";
        const string password = "Password1!";

        var register = new
        {
            Email = email,
            FirstName = "Fresh",
            LastName = "User",
            Password = password,
            DealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId)
        };
        var registerResponse = await client.PostAsJsonAsync("/api/v1/users/register", register, IntegrationTestHelpers.JsonOptions);
        registerResponse.EnsureSuccessStatusCode();

        var login = new { Email = email, Password = password };
        var loginResponse = await client.PostAsJsonAsync("/api/v1/users/login", login, IntegrationTestHelpers.JsonOptions);
        loginResponse.EnsureSuccessStatusCode();
        var result = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(IntegrationTestHelpers.JsonOptions);
        return result!.Token;
    }

    private sealed record LoginResponse(string Token);

    private static async Task<Guid> SeedClientAsync(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        var entity = new Client(dealerId, "Perm", "Test", "444", "perm.test@example.com", "888", "Calle 2", DateTime.UtcNow);
        db.Clients.Add(entity);
        await db.SaveChangesAsync();
        return entity.Id;
    }

    [Fact]
    public async Task UploadDocument_WithoutDocumentsCreatePermission_Returns403()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var clientId = await SeedClientAsync(factory);

        var token = await RegisterAndLoginFreshUserAsync(factory);
        var httpClient = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(httpClient, token);

        var request = new
        {
            ClientId = clientId,
            Type = DocumentType.DNI,
            Base64Content = ValidBase64Content,
            FileName = "dni.png",
            ContentType = "image/png"
        };

        var response = await httpClient.PostAsJsonAsync("/api/v1/documents/upload", request, IntegrationTestHelpers.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a user without documents:create must not be able to upload, now that PR6's grants are live");
    }

    [Fact]
    public async Task UploadDocument_ThenDownloadOwnUpload_Returns200Both()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var clientId = await SeedClientAsync(factory);

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var httpClient = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(httpClient, token);

        var uploadRequest = new
        {
            ClientId = clientId,
            Type = DocumentType.DNI,
            Base64Content = ValidBase64Content,
            FileName = "dni.png",
            ContentType = "image/png"
        };

        var uploadResponse = await httpClient.PostAsJsonAsync("/api/v1/documents/upload", uploadRequest, IntegrationTestHelpers.JsonOptions);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "an Admin token carries both documents:create and documents:read from PR6's grants");

        var uploadBody = await uploadResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(IntegrationTestHelpers.JsonOptions);
        var documentId = uploadBody.GetProperty("documentId").GetGuid();

        var downloadResponse = await httpClient.GetAsync($"/api/v1/documents/{documentId}/download-url");

        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "closes the audit's 403 asymmetry — the same role that can upload must also be able to download its own upload");
    }

    [Fact]
    public async Task OcrUpload_WithoutDocumentsCreatePermission_Returns403NotFifteen()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();

        var token = await RegisterAndLoginFreshUserAsync(factory);
        var httpClient = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(httpClient, token);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3 });
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "x.png");

        var response = await httpClient.PostAsync("/api/v1/documents/ocr-upload", content);

        // Finding 3 (design D7): ocr-upload is already gated on DocumentsCreate
        // (Upload.cs:52). An unpermissioned caller must 403 — the audit's observed 415
        // was for a *different* caller shape (see OcrUpload_JsonBodyFromPermissionedUser_Returns415).
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OcrUpload_JsonBodyFromPermissionedUser_Returns415()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var httpClient = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(httpClient, token);

        // ocr-upload only Accepts multipart/form-data. A permissioned caller sending a JSON
        // body must 415 — this is the audit's observed finding, and it is correct HTTP for a
        // multipart-only endpoint, not a defect (proposal §3.5 / spec Out Of Scope).
        var response = await httpClient.PostAsJsonAsync("/api/v1/documents/ocr-upload", new { }, IntegrationTestHelpers.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }
}
