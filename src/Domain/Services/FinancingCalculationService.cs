namespace Domain.Services;

public sealed class FinancingCalculationService
{
    public FinancingResult CalculateFrenchAmortization(
        decimal principal,
        decimal annualRate,
        int installments)
    {
        if (annualRate <= 0)
            return new FinancingResult(principal, principal, 0m, 0m);

        // CFT = (1 + TEA/12)^12 - 1 where TEA = TNA
        var tea = annualRate;
        var cft = Math.Pow(1 + (double)tea / 12, 12) - 1;
        var monthlyRate = cft / 12;

        var onePlusR = Math.Pow(1 + monthlyRate, installments);
        var monthlyPayment = (double)principal * (monthlyRate * onePlusR) / (onePlusR - 1);

        return new FinancingResult(
            MonthlyPayment: Math.Round((decimal)monthlyPayment, 2),
            TotalWithInterest: Math.Round((decimal)monthlyPayment * installments, 2),
            CFT: Math.Round((decimal)(cft * 100), 2),
            TEA: Math.Round(tea * 100, 2)
        );
    }
}

public record FinancingResult(
    decimal MonthlyPayment,
    decimal TotalWithInterest,
    decimal CFT,
    decimal TEA
);