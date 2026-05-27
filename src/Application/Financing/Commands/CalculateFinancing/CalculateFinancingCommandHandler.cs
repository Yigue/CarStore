using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Financing.Dtos;
using Domain.Services;
using SharedKernel;

namespace Application.Financing.Commands.CalculateFinancing;

internal sealed class CalculateFinancingCommandHandler : ICommandHandler<CalculateFinancingCommand, FinancingCalculationResponse>
{
    private readonly ICurrentTenantService _tenantService;
    private readonly FinancingCalculationService _calculationService;

    public CalculateFinancingCommandHandler(
        ICurrentTenantService tenantService,
        FinancingCalculationService calculationService)
    {
        _tenantService = tenantService;
        _calculationService = calculationService;
    }

    public async Task<Result<FinancingCalculationResponse>> Handle(
        CalculateFinancingCommand request,
        CancellationToken ct)
    {
        var installments = request.Installments;
        if (installments <= 0 || installments > 84)
            return Result.Failure<FinancingCalculationResponse>(new Error("Financing.InvalidInstallments", "Las cuotas deben ser entre 1 y 84", ErrorType.Validation));

        if (request.VehiclePrice <= 0)
            return Result.Failure<FinancingCalculationResponse>(new Error("Financing.InvalidPrice", "El precio debe ser mayor a 0", ErrorType.Validation));

        // TNA: use override if provided, otherwise use dealer settings TNA (default 72%)
        var tna = request.TnaOverride ?? 0.72m;

        var result = _calculationService.CalculateFrenchAmortization(request.VehiclePrice, tna, installments);

        return Result.Success(new FinancingCalculationResponse(
            result.MonthlyPayment,
            result.TotalWithInterest,
            result.CFT,
            result.TEA
        ));
    }
}