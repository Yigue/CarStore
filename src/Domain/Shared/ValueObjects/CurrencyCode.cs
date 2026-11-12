using SharedKernel;

namespace Domain.Shared.ValueObjects;

/// <summary>
/// Closed set of ISO-4217 currencies the platform supports (ADR C1, saas-functional-completeness).
/// Adding a currency is a deliberate change here, never a free-form string typed by a caller.
/// </summary>
public sealed record CurrencyCode
{
    public static readonly CurrencyCode Ars = new("ARS");
    public static readonly CurrencyCode Usd = new("USD");

    private static readonly CurrencyCode[] All = [Ars, Usd];

    public string Value { get; }

    private CurrencyCode(string value) => Value = value;

    public static IReadOnlyList<CurrencyCode> Supported => All;

    /// <summary>Normalizes to upper case and rejects anything outside the closed set.</summary>
#pragma warning disable CA1065 // Do not raise exceptions in unexpected locations
    public static CurrencyCode Parse(string? value)
    {
        if (!TryParse(value, out CurrencyCode? code))
            throw new DomainException($"Unsupported currency '{value}'. Supported: {string.Join(", ", All.Select(c => c.Value))}");

        return code;
    }


    public static bool TryParse(string? value, out CurrencyCode code)
    {
        string normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        code = All.FirstOrDefault(c => c.Value == normalized)!;
        return code is not null;
    }

    public override string ToString() => Value;
}
