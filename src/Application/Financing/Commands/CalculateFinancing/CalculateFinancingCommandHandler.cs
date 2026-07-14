using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Financing.Dtos;
using Domain.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Financing.Commands.CalculateFinancing;

internal sealed class CalculateFinancingCommandHandler : ICommandHandler<CalculateFinancingCommand, FinancingCalculationResponse>
{
    // Default TNA fallback (whole percentage, same scale as DealerSettings.InterestRateTna)
    // used only when the tenant has no DealerSettings row configured yet.
    private const decimal DefaultTnaPercentage = 72m;

    private readonly ICurrentTenantService _tenantService;
    private readonly IApplicationDbContext _context;
    private readonly FinancingCalculationService _calculationService;

    public CalculateFinancingCommandHandler(
        ICurrentTenantService tenantService,
        IApplicationDbContext context,
        FinancingCalculationService calculationService)
    {
        _tenantService = tenantService;
        _context = context;
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

        // TNA: use override if provided (and non-zero), otherwise resolve the
        // Dealer's configured rate via ICurrentTenantService. Both are stored
        // and received as whole percentages (e.g. 72, 65.50) — normalization
        // to fraction (÷100) happens HERE, in a single place, before invoking
        // the domain service (SDD Parte 1 — Requisito 1).
        var tnaPercentage = request.TnaOverride is > 0
            ? request.TnaOverride.Value
            : await ResolveDealerTnaPercentageAsync(ct);

        var tnaFraction = tnaPercentage / 100m;

        var result = _calculationService.CalculateFrenchAmortization(request.VehiclePrice, tnaFraction, installments);

        return Result.Success(new FinancingCalculationResponse(
            result.MonthlyPayment,
            result.TotalWithInterest,
            result.CFT,
            result.TEA
        ));
    }

    private async Task<decimal> ResolveDealerTnaPercentageAsync(CancellationToken ct)
    {
        var interestRateTna = await _context.DealerSettings
            .Where(s => s.DealerId == _tenantService.DealerId)
            .Select(s => s.InterestRateTna)
            .SingleOrDefaultAsync(ct);

        return interestRateTna is > 0 ? interestRateTna.Value : DefaultTnaPercentage;
    }
}