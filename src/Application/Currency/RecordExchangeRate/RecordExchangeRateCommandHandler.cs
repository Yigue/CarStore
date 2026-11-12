using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Currency;
using Domain.Shared.ValueObjects;
using SharedKernel;

namespace Application.Currency.RecordExchangeRate;

/// <summary>sfc B0.2.3: records an append-only manual exchange rate for the caller's dealer, with the actor as creator.</summary>
internal sealed class RecordExchangeRateCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    ICurrentTenantService tenantService)
    : ICommandHandler<RecordExchangeRateCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RecordExchangeRateCommand request, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
        {
            return Result.Failure<Guid>(guard.Error);
        }

        if (!CurrencyCode.TryParse(request.BaseCurrency, out CurrencyCode baseCurrency)
            || !CurrencyCode.TryParse(request.QuoteCurrency, out CurrencyCode quoteCurrency))
        {
            return Result.Failure<Guid>(Error.Validation(
                "Currency.Unsupported", "Unsupported currency. Supported: ARS, USD."));
        }

        Result<DealerExchangeRate> recorded = DealerExchangeRate.Record(
            tenantService.DealerId,
            baseCurrency,
            quoteCurrency,
            request.Rate,
            request.EffectiveAtUtc,
            userContext.UserId);

        if (recorded.IsFailure)
        {
            return Result.Failure<Guid>(recorded.Error);
        }

        context.DealerExchangeRates.Add(recorded.Value);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(recorded.Value.Id);
    }
}
