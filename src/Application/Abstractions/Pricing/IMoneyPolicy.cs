namespace Application.Abstractions.Pricing;

/// <summary>
/// Product switches for the money engine (sfc B0.2.3 / VB.2).
/// </summary>
public interface IMoneyPolicy
{
    /// <summary>
    /// When true, creating a Car, Quote or Sale without an explicit <c>currency</c> is rejected (400).
    /// Off by default: API clients that predate the currency columns keep working (ARS default).
    /// Configured with <c>Money:RequireExplicitCurrency</c>.
    /// </summary>
    bool RequireExplicitCurrency { get; }
}
