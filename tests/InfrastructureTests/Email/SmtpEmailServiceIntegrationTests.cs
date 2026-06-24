using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace InfrastructureTests.Email;

/// <summary>
/// Integration test: sends an email through a real MailHog SMTP container
/// and verifies delivery via MailHog's HTTP API.
///
/// Gated with [Trait("Category", "Integration")] — excluded from unit CI runs:
///   dotnet test --filter "Category!=Integration"
///
/// Requires Docker. Run manually or in a CI environment with Docker available.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SmtpEmailServiceIntegrationTests : IAsyncLifetime
{
    private IContainer? _mailHog;
    private int _smtpPort;
    private int _httpPort;

    public async Task InitializeAsync()
    {
        // Use generic container — MailHog has no first-class Testcontainers module.
        _mailHog = new ContainerBuilder()
            .WithImage("mailhog/mailhog:latest")
            .WithPortBinding(1025, assignRandomHostPort: true)
            .WithPortBinding(8025, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1025))
            .Build();

        await _mailHog.StartAsync();

        _smtpPort = _mailHog.GetMappedPublicPort(1025);
        _httpPort = _mailHog.GetMappedPublicPort(8025);
    }

    public async Task DisposeAsync()
    {
        if (_mailHog is not null)
        {
            await _mailHog.DisposeAsync();
        }
    }

    [Fact]
    public async Task SendEmailAsync_DeliversToMailHog()
    {
        // Arrange
        var emailOptions = Options.Create(new EmailOptions
        {
            Host = "localhost",
            Port = _smtpPort,
            FromAddress = "test@carstore.local",
            FromName = "CarStore Test",
            UseStartTls = false
        });

        var logger = NullLogger<SmtpEmailService>.Instance;
        var sut = new SmtpEmailService(emailOptions, logger);

        // Act
        await sut.SendEmailAsync(
            to: "recipient@example.com",
            subject: "Integration Test Subject",
            body: "<p>Hello world from integration test</p>",
            cancellationToken: CancellationToken.None);

        // Assert — query MailHog HTTP API to verify delivery
        using var http = new HttpClient();
        http.BaseAddress = new Uri($"http://localhost:{_httpPort}");

        // Give MailHog a moment to index the message
        await Task.Delay(500);

        var response = await http.GetAsync("/api/v2/messages");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<MailHogMessages>();
        content.Should().NotBeNull();
        content!.Total.Should().Be(1, "exactly one message should have been delivered");

        var firstItem = content.Items.Should().ContainSingle().Subject;
        firstItem.Content.Headers["To"].Should().ContainMatch(
            "*recipient@example.com*",
            "recipient address should appear in the To header");
        firstItem.Content.Headers["Subject"].Should().ContainMatch(
            "*Integration Test Subject*",
            "subject should match what was sent");
    }

    // --- MailHog response DTOs ---

    private sealed record MailHogMessages(
        [property: System.Text.Json.Serialization.JsonPropertyName("total")] int Total,
        [property: System.Text.Json.Serialization.JsonPropertyName("items")] MailHogMessage[] Items);

    private sealed record MailHogMessage(
        [property: System.Text.Json.Serialization.JsonPropertyName("Content")] MailHogContent Content);

    private sealed record MailHogContent(
        [property: System.Text.Json.Serialization.JsonPropertyName("Headers")] Dictionary<string, string[]> Headers);
}
