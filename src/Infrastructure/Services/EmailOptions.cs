using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Services;

/// <summary>
/// Strongly-typed options bound from the <c>Email:Smtp</c> configuration section.
/// Only validated when the section is present (optional-service pattern — mirrors IBlobStorageService/IOcrService).
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email:Smtp";

    /// <summary>SMTP hostname. Required when email is enabled.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Host { get; set; } = string.Empty;

    /// <summary>SMTP port. Defaults to 587 (STARTTLS).</summary>
    public int Port { get; set; } = 587;

    /// <summary>SMTP username. Optional — leave empty for unauthenticated relays (e.g. MailHog).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>SMTP password. Optional — from environment variable or secret store in production. Never commit.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Sender address that appears in the From header. Required when email is enabled.</summary>
    [Required(AllowEmptyStrings = false)]
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Sender display name that appears in the From header.</summary>
    public string FromName { get; set; } = "CarStore";

    /// <summary>When true, negotiates STARTTLS on the configured port (default 587). Set to false for plaintext relays (e.g. MailHog on port 1025).</summary>
    public bool UseStartTls { get; set; } = true;
}
