using System.Threading;
using System.Threading.Tasks;

namespace Application.Abstractions.Security;

/// <summary>
/// Verifies a CAPTCHA/anti-bot challenge token against the configured provider.
/// spr PR 3.3 (B10): guards anonymous write endpoints (e.g. <c>POST dealers/provision</c>) against
/// automated abuse. The production implementation (<c>TurnstileCaptchaVerifier</c>) MUST fail closed
/// — a provider timeout or outage returns <see cref="CaptchaVerificationResult.Unavailable"/>, never
/// <see cref="CaptchaVerificationResult.Valid"/>.
/// </summary>
public interface ICaptchaVerifier
{
    Task<CaptchaVerificationResult> VerifyAsync(string token, CancellationToken cancellationToken = default);
}

/// <summary>Outcome of a CAPTCHA verification attempt.</summary>
public enum CaptchaVerificationResult
{
    /// <summary>Token accepted by the provider.</summary>
    Valid,

    /// <summary>Token rejected by the provider (wrong/expired/already-used token).</summary>
    Invalid,

    /// <summary>The provider could not be reached (timeout, 5xx, network failure). Callers MUST treat
    /// this as a rejection (fail closed) — never as implicit success.</summary>
    Unavailable
}
