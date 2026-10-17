using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Application.Currency;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Currency;
using Domain.Deposits;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Leads;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Deposits.Create;

/// <summary>
/// sfc B1.3.5. The aggregate enforces RN-001..RN-004 from the facts loaded here; the partial unique index
/// <c>ux_deposits_one_active_per_car</c> settles a race between two requests (mapped to the same 409).
/// </summary>
internal sealed class CreateDepositCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    ICurrentTenantService tenantService,
    ICachedCategoryService cachedCategoryService)
    : ICommandHandler<CreateDepositCommand, Guid>
{
    public const string IncomeCategoryName = "Seña";

    public async Task<Result<Guid>> Handle(CreateDepositCommand command, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return Result.Failure<Guid>(guard.Error);

        Guid dealerId = tenantService.DealerId;
        DateTime now = dateTimeProvider.UtcNow;

        Car? car = await context.Cars.FirstOrDefaultAsync(c => c.Id == command.CarId, cancellationToken);
        if (car is null || car.DealerId != dealerId)
            return Result.Failure<Guid>(CarErrors.NotFound(command.CarId));

        Client? client = null;
        if (command.ClientId is { } clientId)
        {
            client = await context.Clients.FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);
            if (client is null || client.DealerId != dealerId)
                return Result.Failure<Guid>(ClientErrors.NotFound(clientId));
        }

        // Security review: a deposit may only reference this dealer's lead, and a quote of this dealer over this car.
        // The expiry sweep runs without a tenant and emails the referenced buyer, so a foreign id must never get in.
        if (command.LeadId is { } leadId
            && !await context.Leads.AnyAsync(l => l.Id == leadId && l.DealerId == dealerId, cancellationToken))
            return Result.Failure<Guid>(LeadErrors.NotFound(leadId));

        if (command.QuoteId is { } quoteId
            && !await context.Quotes.AnyAsync(q => q.Id == quoteId && q.DealerId == dealerId && q.CarId == car.Id, cancellationToken))
            return Result.Failure<Guid>(QuoteErrors.NotFound(quoteId));

        if (string.IsNullOrWhiteSpace(command.Currency))
            return Result.Failure<Guid>(DepositErrors.CurrencyRequired());
        if (!CurrencyCode.TryParse(command.Currency, out CurrencyCode depositCurrency))
            return Result.Failure<Guid>(Error.Validation("Currency.Unsupported", "Unsupported currency. Supported: ARS, USD."));

        CurrencyCode carCurrency = CurrencyCode.Parse(car.Price.Currency);
        DealerExchangeRate? rate = depositCurrency != carCurrency
            ? await context.FindLatestEffectiveRateAsync(dealerId, carCurrency, depositCurrency, now, cancellationToken)
            : null;
        Result<ExchangeRateSnapshot?> snapshot = ExchangeRateSnapshot.Capture(carCurrency, depositCurrency, rate, now);
        if (snapshot.IsFailure)
            return Result.Failure<Guid>(snapshot.Error);

        bool hasActiveDeposit = await context.Deposits.AnyAsync(
            d => d.CarId == car.Id && d.Status == DepositStatus.Active, cancellationToken);
        Guid? reservedByQuoteId = await context.Quotes
            .Where(q => q.CarId == car.Id && q.Status == QuoteStatus.Accepted)
            .Select(q => (Guid?)q.Id)
            .FirstOrDefaultAsync(cancellationToken);
        int validityDays = await context.DealerSettings
            .Where(d => d.DealerId == dealerId)
            .Select(d => (int?)d.DepositValidityDays)
            .FirstOrDefaultAsync(cancellationToken) ?? Deposit.DefaultValidityDays;

        Result<Deposit> created = Deposit.Create(
            dealerId,
            car.Id,
            command.ClientId,
            command.QuoteId,
            command.Amount,
            depositCurrency.Value,
            snapshot.Value,
            now,
            new DepositCarContext(car.ServiceCar, reservedByQuoteId, hasActiveDeposit, carCurrency.Value),
            validityDays,
            leadId: command.LeadId);
        if (created.IsFailure)
            return Result.Failure<Guid>(created.Error);

        Deposit deposit = created.Value;
        context.Deposits.Add(deposit);

        // RN-001: a Disponible car becomes Reservado; one already reserved by this same quote stays as it is.
        if (car.ServiceCar == StatusServiceCar.Disponible)
            car.Reserve(now);

        TransactionCategory category = await IncomeCategoryAsync(cancellationToken);
        context.Transactions.Add(new FinancialTransaction(
            dealerId,
            TransactionType.Income,
            deposit.Amount,
            $"Seña - vehículo {car.Patente.Value}",
            command.PaymentMethod,
            category,
            car: car,
            client: client,
            transactionDate: now,
            sourceId: deposit.Id,
            exchangeRate: deposit.ExchangeRate)); // sfc B0.3.1 / ADR C2

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost the race on ux_deposits_one_active_per_car (RN-004).
            return Result.Failure<Guid>(DepositErrors.AlreadyActive(car.Id));
        }

        return Result.Success(deposit.Id);
    }

    private async Task<TransactionCategory> IncomeCategoryAsync(CancellationToken cancellationToken)
    {
        TransactionCategory? category = await cachedCategoryService.GetByNameAsync(IncomeCategoryName, cancellationToken);
        if (category is not null)
            return category;

        category = new TransactionCategory(IncomeCategoryName, "Señas recibidas por vehículos", TransactionType.Income);
        context.TransactionCategories.Add(category);
        await cachedCategoryService.InvalidateCacheAsync(cancellationToken);
        return category;
    }
}
