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
        // que un valor inválido llegue al cálculo y produzca un overflow.
        if (annualRate <= 0)
            throw new DomainException("La tasa (TNA) debe ser mayor a 0.");

        if (installments <= 0)
            throw new DomainException("La cantidad de cuotas debe ser mayor a 0.");

        try
        {
            // CFT = (1 + TEA/12)^12 - 1 where TEA = TNA (pure decimal, integer exponents)
            var tea = annualRate;
            var cft = DecimalMath.PowInt(1 + tea / 12, 12) - 1;
            var monthlyRate = cft / 12;

            var onePlusR = DecimalMath.PowInt(1 + monthlyRate, installments);
            var monthlyPayment = principal * (monthlyRate * onePlusR) / (onePlusR - 1);

            return new FinancingResult(
                MonthlyPayment: Math.Round(monthlyPayment, 2),
                TotalWithInterest: Math.Round(monthlyPayment * installments, 2),
                CFT: Math.Round(cft * 100, 2),
                TEA: Math.Round(tea * 100, 2)
            );
        }
        catch (OverflowException)
        {
            throw new DomainException("Los parámetros de financiación exceden el rango de cálculo permitido.");
        }
    }
}

public record FinancingResult(
    decimal MonthlyPayment,
    decimal TotalWithInterest,
    decimal CFT,
    decimal TEA
);