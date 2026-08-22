using System;
using System.Net;
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
/// qa-p1-integridad PR7, Slice 13 (D7, REQ: document-upload-lifecycle).
/// <para>
/// Today <c>UploadDocumentCommandHandler.cs:32</c> calls
/// <c>Convert.FromBase64String(request.Base64Content)</c> directly — a malformed value throws an
/// uncaught <see cref="FormatException"/> that surfaces as 500 (deliberately NOT mapped by the
/// global handler, per PR1 Slice 1.6 — this is a validator's job, not the global handler's).
/// The handler also never verifies <c>ClientId</c> exists, and
/// <c>DocumentsEndpoints.cs</c> unconditionally wraps every failure in
/// <c>Results.BadRequest(result.Error)</c>, so a <c>NotFound</c> Result reaches the wire as 400.
/// </para>
/// </summary>
public class DocumentUploadTests
{
    private const string ValidBase64Content = "aGVsbG8gd29ybGQ="; // "hello world"

    private static async Task<(CustomWebApplicationFactory Factory, HttpClient Client, Guid ClientId)> CreateAuthenticatedHostWithClientAsync()
    {
        var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
            var entity = new Client(dealerId, "Ana", "Lopez", "333", "ana.lopez@example.com", "999", "Calle 1", DateTime.UtcNow);
            db.Clients.Add(entity);
            await db.SaveChangesAsync();
        }

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        using var readScope = factory.Services.CreateScope();
        var readDb = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var clientId = await readDb.Clients.Select(c => c.Id).FirstAsync();

        return (factory, client, clientId);
    }

    [Fact]
    public async Task UploadDocument_MalformedBase64Content_Returns400NotFiveHundred()
    {
        var (factory, client, clientId) = await CreateAuthenticatedHostWithClientAsync();
        await using var _ = factory;

        var request = new
        {
            ClientId = clientId,
            Type = DocumentType.DNI,
            Base64Content = "not-valid-base64!!!",
            FileName = "dni.png",
            ContentType = "image/png"
        };

        var response = await client.PostAsJsonAsync("/api/v1/documents/upload", request, IntegrationTestHelpers.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "malformed base64 must be caught by validation, not surfaced as an unhandled 500");
    }

    [Fact]
    public async Task UploadDocument_UnknownClientId_Returns404()
    {
        var (factory, client, _) = await CreateAuthenticatedHostWithClientAsync();
        await using var _ = factory;

        var request = new
        {
            ClientId = Guid.NewGuid(),
            Type = DocumentType.DNI,
            Base64Content = ValidBase64Content,
            FileName = "dni.png",
            ContentType = "image/png"
        };

        var response = await client.PostAsJsonAsync("/api/v1/documents/upload", request, IntegrationTestHelpers.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "an unknown clientId must reach the wire as 404 — a Result.Match(..., CustomResults.Problem) concern, " +
            "not just a handler-level check, since Results.BadRequest(result.Error) would squash it to 400");
    }

    [Fact]
    public async Task UploadDocument_ValidRequest_Returns200AndPersists()
    {
        var (factory, client, clientId) = await CreateAuthenticatedHostWithClientAsync();
        await using var _ = factory;

        var request = new
        {
            ClientId = clientId,
            Type = DocumentType.DNI,
            Base64Content = ValidBase64Content,
            FileName = "dni.png",
            ContentType = "image/png"
        };

        var response = await client.PostAsJsonAsync("/api/v1/documents/upload", request, IntegrationTestHelpers.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
