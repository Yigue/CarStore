using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Cars.Events;
using Domain.Clients;
using Domain.Clients.Attributes;
using Domain.Leads;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Domain.Sales;
using Domain.Sales.Attributes;
using Domain.Sales.Events;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

// Both namespaces define a PaymentMethod and this file has to speak about both: what the buyer
// INTENDED on the quote, and how the money actually moves on the sale. Bare `PaymentMethod` in
// here resolves to the quote's, so name both explicitly rather than rely on which using wins.
using QuotePaymentMethod = Domain.Quotes.Attributes.PaymentMethod;
using SalePaymentMethod = Domain.Financial.Attributes.PaymentMethod;

namespace Application.Sales.Create;

internal sealed class CreateSaleCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    ICurrentTenantService tenantService)
    : ICommandHandler<CreateSaleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateSaleCommand command, CancellationToken cancellationToken)
    {
        // Verify if car exists and is available
        Car? car = await context.Cars
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == command.CarId, cancellationToken);      
        if (car == null)
        {
            return Result.Failure<Guid>(CarErrors.NotFound(command.CarId));
        }        
        // Validate car does not already have a completed sale in Sales records.
        bool hasCompletedSale = await context.Sales
            .AnyAsync(s => s.CarId == command.CarId && s.Status == SaleStatus.Completed, cancellationToken);
        if (hasCompletedSale)
        {
            return Result.Failure<Guid>(CarErrors.AlreadySold(command.CarId));
        }

        // Validate car is available (only check ServiceCar, as CarStatus is about condition, not availability).
        // D-1: un vehículo Reservado (tomado por la cotización que se está convirtiendo) también es vendible.
        if (car.ServiceCar != StatusServiceCar.Disponible && car.ServiceCar != StatusServiceCar.Reservado)
        {
            return Result.Failure<Guid>(CarErrors.AlreadySold(command.CarId));
        }

        // D-5: a quote converts into at most one sale. Guard against a second sale created
        // from the same quote (idempotency on the quote -> sale conversion). Sales are
        // tenant-scoped, so the default query filter keeps this within the dealer.
        Quote? acceptedQuote = null;

        if (command.QuoteId is { } quoteId)
        {
            bool alreadyConverted = await context.Sales
                .AnyAsync(s => s.QuoteId == quoteId, cancellationToken);

            if (alreadyConverted)
            {
                return Result.Failure<Guid>(SalesErrors.AlreadyConvertedFromQuote(quoteId));
            }

            // REQ-SL-QCASCADE-001: a supplied QuoteId must reference an Accepted quote whose
            // car and party (client or lead) match this command. Tenant-scoped by the default
            // query filter, consistent with the rest of this handler.
            Quote? quote = await context.Quotes
                .FirstOrDefaultAsync(q => q.Id == quoteId, cancellationToken);

            if (quote is null)
            {
                return Result.Failure<Guid>(SalesErrors.QuoteNotFound(quoteId));
            }

            if (quote.Status != QuoteStatus.Accepted)
            {
                return Result.Failure<Guid>(SalesErrors.QuoteNotAccepted(quoteId));
            }

            if (quote.CarId != command.CarId)
            {
                return Result.Failure<Guid>(SalesErrors.QuoteMismatch(quoteId));
            }

            // Party consistency: a client-linked quote must match the sale's client; a
            // lead-linked quote (client created at conversion time) matches on the lead instead.
            bool partyMatches = quote.ClientId is { } qClientId
                ? qClientId == command.ClientId
                : quote.LeadId == command.LeadId;

            if (!partyMatches)
            {
                return Result.Failure<Guid>(SalesErrors.QuoteMismatch(quoteId));
            }

            acceptedQuote = quote;
        }

        // Verify if client exists
        Client? client = await context.Clients.FindAsync(new object[] { command.ClientId }, cancellationToken);
        if (client == null)
        {
            return Result.Failure<Guid>(ClientErrors.NotFound(command.ClientId));
        }
        
        // Reject only a client there is no deal to be had with. Selling is what TURNS a prospect
        // into a client: the one created when a lead reaches Negociación is born Prospect, and
        // Active is stamped by ActivateClientOnSaleCompletedHandler once a sale COMPLETES.
        // Demanding Active here closed the circle — the first sale required the client to have
        // already bought — and left every lead unable to reach Ganado from the board.
        if (client.Status is ClientStatus.Lost or ClientStatus.Inactive)
        {
            return Result.Failure<Guid>(ClientErrors.Inactive(client.Id));
        }
 
        // ── VEN-02: a prospect cannot be invoiced ────────────────────────────────────────────
        //
        // The client the CRM created when the lead reached Negociación has a name, an email and
        // a placeholder DNI. That is enough to quote and not enough to bill. The sale carries the
        // missing identity, so the operator supplies it once, at the moment it is actually
        // needed, instead of being bounced to the client screen and back.
        if (!client.HasBillingData)
        {
            if (string.IsNullOrWhiteSpace(command.ClientDni) || string.IsNullOrWhiteSpace(command.ClientAddress))
            {
                return Result.Failure<Guid>(SalesErrors.ClientDataIncomplete(client.Id));
            }

            Result completion = client.CompleteBillingData(
                command.ClientDni,
                command.ClientAddress,
                dateTimeProvider.UtcNow);

            if (completion.IsFailure)
            {
                return Result.Failure<Guid>(completion.Error);
            }

            // Selling is what turns a prospect into a customer. ActivateClientOnSaleCompletedHandler
            // still does this off the outbox when the sale COMPLETES; doing it here means a client
            // whose identity was just completed is already usable for the rest of this request.
            client.Activate();
        }

        // ── VEN-04: the sale inherits what the accepted quote agreed ─────────────────────────
        //
        // The price and the payment arrangement were negotiated on the quote. Re-typing them into
        // the sale form is how a vehicle gets invoiced at a number nobody promised — and the two
        // entities do not even share an enum for "forma de pago", so the front end had been
        // defaulting to Efectivo regardless of what was agreed. Explicit values still win: the
        // final price can legitimately differ from the offer.
        decimal? finalPrice = command.FinalPrice;
        SalePaymentMethod? paymentMethod = command.PaymentMethod;

        if (acceptedQuote is not null)
        {
            finalPrice ??= acceptedQuote.ProposedPrice.Amount;
            paymentMethod ??= MapQuotePaymentMethod(acceptedQuote.PaymentMethod);
        }

        if (finalPrice is not { } agreedPrice || paymentMethod is not { } agreedMethod)
        {
            // Unreachable through the validator, which requires both whenever there is no quote
            // to inherit from. Kept so the aggregate is never handed a price it cannot honour.
            return Result.Failure<Guid>(SalesErrors.PriceRequired());
        }

        var sale = new Sale(
            tenantService.DealerId,
            command.CarId,
            command.ClientId,
            agreedPrice,
            agreedMethod,
            command.ContractNumber,
            command.Comments,
            dateTimeProvider.UtcNow,
            command.LeadId,
            command.QuoteId,
            command.SalespersonId
            );

        // VEN-03: how it is paid and what paperwork backs it. Both are optional — a cash sale
        // recorded in one line has nothing to add here.
        Result paymentTerms = sale.SetPaymentTerms(
            command.DownPayment,
            command.TradeInCarId,
            command.TradeInValue,
            command.FinancedAmount,
            command.InstallmentCount,
            command.InstallmentAmount,
            command.FinancingEntity);

        if (paymentTerms.IsFailure)
        {
            return Result.Failure<Guid>(paymentTerms.Error);
        }

        sale.SetLegalDocuments(
            command.InvoiceNumber,
            command.TransferFormNumber,
            command.RegistrationNumber,
            command.DeliveryDate);

        // Sales are Pending by default. They are only force-completed when the
        // caller explicitly requests it (e.g. a legacy/cash sale recorded as
        // already settled). Cancelled as an initial status is rejected by the validator.
        SaleStatus requestedStatus = command.Status ?? SaleStatus.Pending;

        if (requestedStatus == SaleStatus.Completed)
        {
            context.Sales.Add(sale);

            // Complete the sale to trigger financial transaction and domain events
            sale.Complete();

            // CAT-01: mark the unit sold HERE, in the same transaction as the sale.
            // SaleCompletedCarStatusHandler does the same thing off the outbox, but that is a
            // Quartz job on a tick — until it runs (or if it never does, because the job is down
            // or its message errored) the car keeps its old service status and the public
            // catalogue, which filters on exactly that column, keeps offering a vehicle the
            // dealership has already sold. The handler stays as the idempotent safety net:
            // MarkAsSold is a no-op once ServiceCar is Vendido.
            car.MarkAsSold(dateTimeProvider.UtcNow);
        }
        else
        {
            context.Sales.Add(sale);

            // Pending sale: reserve the car instead of marking it sold outright.
            // If it's already Reservado (e.g. converted from an accepted quote,
            // see D-1), leave it as-is — Reserve() would otherwise throw.
            if (car.ServiceCar == StatusServiceCar.Disponible)
            {
                car.Reserve(dateTimeProvider.UtcNow);
            }
        }

        if (command.LeadId is { } saleLeadId)
        {
            Lead? lead = await context.Leads.FirstOrDefaultAsync(l => l.Id == saleLeadId, cancellationToken);
            if (lead is not null && lead.Status != LeadStatus.Ganado)
            {
                lead.ForceStatus(LeadStatus.Ganado);
            }
        }

        // Single SaveChangesAsync: persists sale, car status, and all domain events in one transaction
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(sale.Id);
    }

    /// <summary>
    /// VEN-04: translates the quote's payment intent into the sale's settlement method.
    ///
    /// <para>
    /// The two live in different enums on purpose — a quote says how the buyer INTENDS to pay
    /// (<c>Domain.Quotes.Attributes.PaymentMethod</c>), a sale records how the money actually
    /// moved (<c>Domain.Financial.Attributes.PaymentMethod</c>). They were never mapped, so the
    /// front end filled in Efectivo whatever had been agreed. Permuta and Mixto have no single
    /// financial instrument behind them: the trade-in and the split are recorded in the payment
    /// breakdown (VEN-03), and the method itself falls to Other rather than inventing one.
    /// </para>
    /// </summary>
    internal static SalePaymentMethod MapQuotePaymentMethod(QuotePaymentMethod quotePaymentMethod) =>
        quotePaymentMethod switch
        {
            QuotePaymentMethod.Contado => SalePaymentMethod.Cash,
            QuotePaymentMethod.Financiado => SalePaymentMethod.BankTransfer,
            QuotePaymentMethod.Permuta => SalePaymentMethod.Other,
            QuotePaymentMethod.Mixto => SalePaymentMethod.Other,
            _ => SalePaymentMethod.Other,
        };
}
