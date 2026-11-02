using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Security;

namespace WebApiTests.Fakes;

/// <summary>
/// Deterministic <see cref="ICaptchaVerifier"/> double. spr PR 3.3 (B10): lets tests exercise the
/// CaptchaEndpointFilter's Invalid/Unavailable branches without hitting the real Turnstile API
/// (that provider-level behavior is covered separately by
/// <c>InfrastructureTests/Security/TurnstileCaptchaVerifierTests.cs</c>).
/// </summary>
public sealed class StubCaptchaVerifier : ICaptchaVerifier
{
    public CaptchaVerificationResult Result { get; set; } = CaptchaVerificationResult.Valid;

    public Task<CaptchaVerificationResult> VerifyAsync(string token, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result);
}
