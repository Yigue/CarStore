using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Services;

/// <summary>
/// Strongly-typed options bound from the <c>Email:Resend</c> configuration section.
/// Only validated when the section is present (optional-service pattern).
/// </summary>
public sealed class ResendOptions
{
    public const string SectionName = "Email:Resend";

    /// <summary>Resend API key. Required when Resend email provider is enabled.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Sender address that appears in the From header.</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Sender display name that appears in the From header.</summary>
    public string FromName { get; set; } = "CarStore";

    /// <summary>Resend API endpoint URL. Defaults to standard Resend emails endpoint.</summary>
    public string ApiUrl { get; set; } = "https://api.resend.com/emails";
}
