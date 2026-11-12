using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Security;

/// <summary>
/// Calls Cloudflare Turnstile's <c>siteverify</c> endpoint to validate a CAPTCHA token.
/// spr PR 3.3 (B10): fails closed — any timeout, network failure or non-2xx response from the
/// provider is reported as <see cref="CaptchaVerificationResult.Unavailable"/>, never treated as a
/// pass. The <see cref="HttpClient"/> is created via <c>IHttpClientFactory</c>
/// (<c>AddHttpClient&lt;TurnstileCaptchaVerifier&gt;</c> in <c>DependencyInjection.cs</c>) with a 3s timeout.
/// </summary>
public sealed class TurnstileCaptchaVerifier(
    HttpClient httpClient,
    IOptions<CaptchaOptions> options,
    ILogger<TurnstileCaptchaVerifier> logger) : ICaptchaVerifier
{
    public async Task<CaptchaVerificationResult> VerifyAsync(string token, CancellationToken cancellationToken = default)
    {
        CaptchaOptions settings = options.Value;

        try
        {
            var payload = new Dictionary<string, string>
            {
                ["secret"] = settings.SecretKey,
                ["response"] = token
            };

            using HttpResponseMessage response = await httpClient.PostAsync(
                settings.SiteVerifyUrl,
                new FormUrlEncodedContent(payload),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Turnstile siteverify returned a non-success status code {StatusCode}. Failing closed.",
                    (int)response.StatusCode);
                return CaptchaVerificationResult.Unavailable;
            }

            TurnstileResponse? body = await response.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken);
            return body is { Success: true } ? CaptchaVerificationResult.Valid : CaptchaVerificationResult.Invalid;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The HttpClient's own timeout (configured in DependencyInjection.cs) fired — the caller's
            // token was NOT cancelled. Fail closed, never treat a timeout as a valid token.
            logger.LogWarning("Turnstile siteverify timed out. Failing closed.");
            return CaptchaVerificationResult.Unavailable;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Turnstile siteverify request failed. Failing closed.");
            return CaptchaVerificationResult.Unavailable;
        }
    }

    private sealed record TurnstileResponse(
        [property: JsonPropertyName("success")] bool Success);
}
