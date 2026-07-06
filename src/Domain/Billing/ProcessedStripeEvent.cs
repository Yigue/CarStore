using System;

namespace Domain.Billing;

public sealed class ProcessedStripeEvent
{
    public string StripeEventId { get; set; } = string.Empty;
    public DateTime ProcessedOnUtc { get; set; }
    public Guid? DealerId { get; set; }
}
