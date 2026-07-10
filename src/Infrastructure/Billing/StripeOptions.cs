using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Billing;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    [Required(AllowEmptyStrings = false)]
    public string SecretKey { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string WebhookSecret { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string PriceId { get; set; } = string.Empty;

    public int TrialDays { get; set; } = 14;
}
