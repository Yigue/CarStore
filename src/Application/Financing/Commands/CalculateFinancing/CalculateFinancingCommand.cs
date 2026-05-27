using Application.Abstractions.Messaging;
using Application.Financing.Dtos;

namespace Application.Financing.Commands.CalculateFinancing;

public sealed record CalculateFinancingCommand(
    decimal VehiclePrice,
    int Installments,
    decimal? TnaOverride
) : ICommand<FinancingCalculationResponse>;