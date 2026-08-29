using Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services;

/// <summary>
/// Resend API implementation of <see cref="IEmailService"/> using HttpClient.
/// </summary>
public class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly ResendOptions _options;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(
        HttpClient httpClient,
        IOptions<ResendOptions> options,
        ILogger<ResendEmailService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            throw new ArgumentException("Recipient email cannot be null or empty.", nameof(to));
        }

        try
        {
            var from = !string.IsNullOrWhiteSpace(_options.FromName) && !string.IsNullOrWhiteSpace(_options.FromAddress)
                ? $"{_options.FromName} <{_options.FromAddress}>"
                : (!string.IsNullOrWhiteSpace(_options.FromAddress) ? _options.FromAddress : "onboarding@resend.dev");

            var payload = new
            {
                from,
                to = new[] { to },
                subject = subject ?? string.Empty,
                html = body ?? string.Empty
            };

            var requestJson = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl ?? "https://api.resend.com/emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Resend API failed to send email to {To}. Status: {StatusCode}, Error: {Error}",
                    to, response.StatusCode, errorBody);
                response.EnsureSuccessStatusCode();
            }

            _logger.LogInformation("Successfully sent email to {To} via Resend", to);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error sending email to {To} via Resend: {Message}", to, ex.Message);
            throw;
        }
    }
}
