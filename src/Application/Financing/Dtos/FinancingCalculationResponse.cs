namespace Application.Financing.Dtos;

public sealed record FinancingCalculationResponse(
    decimal MonthlyPayment,
    decimal TotalWithInterest,
    decimal CFT,
    decimal TEA
);