using System.Net;
using System.Net.Http;
using Application.Abstractions.Security;
using Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InfrastructureTests.Security;

/// <summary>
/// spr PR 3.3 (B10) — task 3.3.2 [RED→GREEN]: <see cref="TurnstileCaptchaVerifier"/> MUST fail closed.
/// A provider timeout, network failure, or non-2xx response is reported as
/// <see cref="CaptchaVerificationResult.Unavailable"/> — the caller (<c>CaptchaEndpointFilter</c>)
/// maps that to HTTP 503, never lets the request through as if the token had been accepted.
/// </summary>
public class TurnstileCaptchaVerifierTests
{
    private static TurnstileCaptchaVerifier CreateVerifier(StubHttpMessageHandler handler) =>
        new(
            new HttpClient(handler),
            Options.Create(new CaptchaOptions { SecretKey = "test-secret", SiteVerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify" }),
            NullLogger<TurnstileCaptchaVerifier>.Instance);

    [Fact]
    public async Task VerifyAsync_WhenProviderConfirmsSuccess_ReturnsValid()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, """{"success":true}"""));
        var verifier = CreateVerifier(handler);

        var result = await verifier.VerifyAsync("good-token");

        result.Should().Be(CaptchaVerificationResult.Valid);
    }

    [Fact]
    public async Task VerifyAsync_WhenProviderRejectsToken_ReturnsInvalid()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, """{"success":false,"error-codes":["invalid-input-response"]}"""));
        var verifier = CreateVerifier(handler);

        var result = await verifier.VerifyAsync("bad-token");

        result.Should().Be(CaptchaVerificationResult.Invalid);
    }

    [Fact]
    public async Task VerifyAsync_WhenProviderReturns5xx_ReturnsUnavailable_NeverBypassesToValid()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.InternalServerError, """{"success":true}"""));
        var verifier = CreateVerifier(handler);

        var result = await verifier.VerifyAsync("any-token");

        result.Should().Be(CaptchaVerificationResult.Unavailable,
            "a 5xx from the provider must fail closed, even if the body claims success");
    }

    [Fact]
    public async Task VerifyAsync_WhenProviderTimesOut_ReturnsUnavailable_NeverBypassesToValid()
    {
        // Simulates the HttpClient-level timeout: the request is cancelled internally (the caller's
        // own CancellationToken was NOT the one that fired), which surfaces as a TaskCanceledException.
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException("Simulated client timeout"));
        var verifier = CreateVerifier(handler);

        var result = await verifier.VerifyAsync("any-token", CancellationToken.None);

        result.Should().Be(CaptchaVerificationResult.Unavailable);
    }

    [Fact]
    public async Task VerifyAsync_WhenNetworkFails_ReturnsUnavailable_NeverBypassesToValid()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("Simulated DNS/connection failure"));
        var verifier = CreateVerifier(handler);

        var result = await verifier.VerifyAsync("any-token");

        result.Should().Be(CaptchaVerificationResult.Unavailable);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        // async (not a direct Task.FromResult(responder(...))) so a thrown exception from `responder`
        // faults the returned Task instead of throwing synchronously out of SendAsync — matching how
        // a real HttpClient transport failure (timeout, DNS, connection reset) actually surfaces.
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Yield();
            return responder(request);
        }
    }
}
