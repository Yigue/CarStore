namespace Application.Financing.Dtos;

public sealed record FinancingCalculationRequest(
    decimal VehiclePrice,
    int Installments,
    decimal? TnaOverride
);