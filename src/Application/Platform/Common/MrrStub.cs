namespace Application.Platform.Common;

/// <summary>
/// Placeholder until the saas-subscription-payments change provides real MRR data.
/// </summary>
public sealed record MrrStub(decimal Value, string Currency, bool IsStub, string StubReason);
