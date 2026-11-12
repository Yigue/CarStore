using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Security;

namespace Infrastructure.Security;

/// <summary>
/// Always-valid CAPTCHA verifier. ONLY wired up when <c>Captcha:SecretKey</c> is not configured
/// (Development/local Testing). <c>Program.cs</c> asserts at startup that this type is never the
/// resolved <see cref="ICaptchaVerifier"/> in a Production environment — see the
/// anti-NoOp guard there (spr PR 3.3 / B10).
/// </summary>
public sealed class NoOpCaptchaVerifier : ICaptchaVerifier
{
    public Task<CaptchaVerificationResult> VerifyAsync(string token, CancellationToken cancellationToken = default) =>
        Task.FromResult(CaptchaVerificationResult.Valid);
}
