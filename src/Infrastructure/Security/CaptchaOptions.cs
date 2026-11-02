namespace Infrastructure.Security;

/// <summary>Cloudflare Turnstile configuration (spr PR 3.3 / B10).</summary>
public sealed class CaptchaOptions
{
    public const string SectionName = "Captcha";

    public string SecretKey { get; set; } = string.Empty;

    public string SiteVerifyUrl { get; set; } = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
}
