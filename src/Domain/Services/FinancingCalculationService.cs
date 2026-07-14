using SharedKernel;

namespace Domain.Services;

public sealed class FinancingCalculationService
{
    public FinancingResult CalculateFrenchAmortization(
        decimal principal,
        decimal annualRate,
        int installments)
    {
        // Guard clauses (SDD Parte 1 — Requisito 2): annualRate llega ya
        // normalizada como fracción (0.72, no 72). Estas validaciones evitan
        // que un valor inválido llegue a Math.Pow y produzca Infinity/NaN
        // o un overflow silencioso.
        if (annualRate <= 0)
            throw new DomainException("La tasa (TNA) debe ser mayor a 0.");

        if (installments <= 0)
            throw new DomainException("La cantidad de cuotas debe ser mayor a 0.");

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