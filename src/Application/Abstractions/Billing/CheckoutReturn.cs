namespace Application.Abstractions.Billing;

/// <summary>
/// spr PR 1.1 (ADR B1/B11 plumbing): optional override of the checkout session's success/cancel URLs.
/// Null properties mean "let the gateway use its configured default" — this PR does not change the
/// actual URLs (still <c>{Frontend:BaseUrl}/dashboard</c>); PR 3.3 wires tenant-aware URLs through here.
/// </summary>
public sealed record CheckoutReturn(string? SuccessUrl = null, string? CancelUrl = null)
{
    public static readonly CheckoutReturn None = new();
}
