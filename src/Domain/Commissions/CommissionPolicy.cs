using System.Text.Json;
using SharedKernel;

namespace Domain.Commissions;

/// <summary>
/// sfc B4.1 / RN-009 — dealer-level commission configuration: one default percentage plus optional
/// per-salesperson overrides. One row per dealer. D-COMMISSION: a dealer with NO row is "not
/// configured" at all (the calling handler passes a null <see cref="CommissionRule"/>, which
/// <see cref="CommissionCalculator"/> treats as 0% / IsConfigured=false) — a row that exists with
/// <see cref="DefaultCommissionRate"/> = 0 is a dealer who explicitly chose 0%, which is different
/// (IsConfigured=true).
/// </summary>
public sealed class CommissionPolicy : Entity
{
    /// <summary>D-COMMISSION: no commission until the dealer explicitly configures a rate.</summary>
    public const decimal DefaultRate = 0m;

    public decimal DefaultCommissionRate { get; private set; } = DefaultRate;

    /// <summary>Per-salesperson overrides, stored as JSON (same technique as DealerSettings.CarouselSlidesJson).</summary>
    private string? OverridesJson { get; set; }

    private CommissionPolicy() { }

    /// <summary>Creates the dealer's policy row, defaulting to <see cref="DefaultRate"/> (0%) until changed.</summary>
    public static CommissionPolicy CreateDefault(Guid dealerId)
    {
        var policy = new CommissionPolicy
        {
            Id = Guid.NewGuid(),
            DefaultCommissionRate = DefaultRate,
        };
        policy.SetDealer(dealerId);
        return policy;
    }

    public Result SetDefaultRate(decimal percent)
    {
        if (percent is < 0m or > 100m)
            return Result.Failure(CommissionErrors.PercentageOutOfRange());

        DefaultCommissionRate = percent;
        return Result.Success();
    }

    /// <summary>Sets (or replaces) the override for one salesperson. RN-009: the override beats the dealer default.</summary>
    public Result SetSalespersonRate(Guid salespersonId, decimal percent)
    {
        if (percent is < 0m or > 100m)
            return Result.Failure(CommissionErrors.PercentageOutOfRange());

        List<SalespersonCommissionRate> overrides = GetOverrides()
            .Where(o => o.SalespersonId != salespersonId)
            .ToList();
        overrides.Add(new SalespersonCommissionRate(salespersonId, percent));
        OverridesJson = JsonSerializer.Serialize(overrides);
        return Result.Success();
    }

    /// <summary>Removes a salesperson's override; future sales fall back to the dealer default.</summary>
    public void RemoveSalespersonRate(Guid salespersonId)
    {
        List<SalespersonCommissionRate> overrides = GetOverrides()
            .Where(o => o.SalespersonId != salespersonId)
            .ToList();
        OverridesJson = overrides.Count == 0 ? null : JsonSerializer.Serialize(overrides);
    }

    public IReadOnlyList<SalespersonCommissionRate> GetOverrides()
    {
        if (string.IsNullOrWhiteSpace(OverridesJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<SalespersonCommissionRate>>(OverridesJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>RN-009: the salesperson's override, if any, beats <see cref="DefaultCommissionRate"/>.</summary>
    public decimal RateFor(Guid? salespersonId)
    {
        if (salespersonId is Guid id)
        {
            SalespersonCommissionRate? match = GetOverrides().FirstOrDefault(o => o.SalespersonId == id);
            if (match is not null)
                return match.Rate;
        }

        return DefaultCommissionRate;
    }
}

/// <summary>Per-salesperson commission override (sfc B4.1 / RN-009).</summary>
public sealed record SalespersonCommissionRate(Guid SalespersonId, decimal Rate);
