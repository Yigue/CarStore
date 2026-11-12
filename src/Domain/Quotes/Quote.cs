using SharedKernel;
using Domain.Cars;
using Domain.Clients;
using Domain.Currency;
using Domain.Leads;
using Domain.Quotes.Attributes;
using Domain.Quotes.Events;
using Domain.Shared.ValueObjects;

namespace Domain.Quotes;

public sealed class Quote : Entity, ISoftDeletable
{
    public Guid CarId { get; private set; }
    public Guid? ClientId { get; private set; }
    public Guid? LeadId { get; private set; }
    /// <summary>sfc B4.3 / RN-018: per-dealer folio (`COT-{yyyy}-{n:D6}`), null until assigned by
    /// <see cref="AssignNumber"/>. Nullable so existing quotes can be backfilled by a migration
    /// instead of breaking every pre-existing <c>new Quote(...)</c> call site.</summary>
    public string? Number { get; private set; }
    public Money ProposedPrice { get; private set; }
    /// <summary>sfc B0.2 / ADR C2: set only when the quote currency differs from the vehicle's; immutable once captured.</summary>
    public ExchangeRateSnapshot? ExchangeRate { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public QuoteStatus Status { get; private set; }
    public DateTime ValidUntil { get; private set; }
    public string Comments { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public Car Car { get; private set; }
    public Client? Client { get; private set; }
    public Lead? Lead { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    private Quote() { }

    public Quote(
        Guid dealerId,
        Car car,
        Client? client,
        Lead? lead,
        decimal proposedPrice,
        PaymentMethod paymentMethod,
        DateTime validUntil,
        string comments,
        DateTime date,
        // sfc B0.2.3 phase 1: ARS when omitted.
        CurrencyCode? proposedCurrency = null,
        // ADR C2: the rate frozen when the quote currency differs from the vehicle's. Never recalculated.
        ExchangeRateSnapshot? exchangeRate = null)
    {
        SetDealer(dealerId);

        // At least one party. Both is legitimate and increasingly the normal case: a lead that
        // has been converted IS a client, and the quote belongs to the person, not to whichever
        // record the CRM happened to be showing when it was raised.
        if (client is null && lead is null)
            throw new DomainException("A quote must have either a Client or a Lead");

        if (validUntil <= date)
            throw new DomainException("ValidUntil must be in the future");

        Car = car;
        CarId = car.Id;

        if (client is not null)
        {
            Client = client;
            ClientId = client.Id;
        }

        if (lead is not null)
        {
            Lead = lead;
            LeadId = lead.Id;
        }

        
        ProposedPrice = Money.Of(proposedPrice, proposedCurrency ?? CurrencyCode.Ars);
        ExchangeRate = exchangeRate;
        PaymentMethod = paymentMethod;
        ValidUntil = validUntil;
        Comments = comments ?? string.Empty;
        Status = QuoteStatus.Pending;
        CreatedAt = date;
        UpdatedAt = date;

        Raise(new QuoteCreatedDomainEvent(Id, CarId, ClientId ?? Guid.Empty, ProposedPrice));
    }

    /// <summary>sfc B4.3 / RN-018: formats the per-dealer folio. Pure — the sequence value itself
    /// comes from IDealerSequenceAllocator, a DB side effect this aggregate cannot own.</summary>
    public static string FormatNumber(int year, long sequenceValue) => $"COT-{year:D4}-{sequenceValue:D6}";

    /// <summary>
    /// Assigns this quote's folio, once, right after a sequence value has been allocated for a
    /// NEW quote. Existing quotes get their Number from the AddQuoteNumbers migration backfill
    /// instead — that path writes the column directly, it does not call this method.
    /// </summary>
    public void AssignNumber(string number)
    {
        if (string.IsNullOrWhiteSpace(number))
            throw new DomainException("Quote number cannot be empty");

        if (Number is not null)
            throw new DomainException("Quote number is already assigned");

        Number = number;
    }

    public void Update(
        decimal proposedPrice,
        DateTime validUntil,
        string comments,
        DateTime updatedAt,
        CurrencyCode? proposedCurrency = null)
    {
        if (Status != QuoteStatus.Pending)
            throw new DomainException("Only pending quotes can be updated");

        if (validUntil <= updatedAt)
            throw new DomainException("ValidUntil must be in the future");

        // Without an explicit currency the quote keeps its own; hard-coding ARS here converted USD quotes silently.
        ProposedPrice = Money.Of(proposedPrice, proposedCurrency ?? CurrencyCode.Parse(ProposedPrice.Currency));
        ValidUntil = validUntil;
        Comments = comments ?? string.Empty;
        UpdatedAt = updatedAt;
    }
    
    public void Update(
        Money proposedPrice,
        DateTime validUntil,
        string comments,
        DateTime updatedAt)
    {
        if (Status != QuoteStatus.Pending)
            throw new DomainException("Only pending quotes can be updated");
        
        if (validUntil <= updatedAt)
            throw new DomainException("ValidUntil must be in the future");
        
        ProposedPrice = proposedPrice;
        ValidUntil = validUntil;
        Comments = comments ?? string.Empty;
        UpdatedAt = updatedAt;
    }
    
    public void Accept(DateTime updatedAt)
    {
        if (Status != QuoteStatus.Pending)
            throw new DomainException("Only pending quotes can be accepted");
        
        if (ValidUntil < updatedAt)
            throw new DomainException("Cannot accept an expired quote");
        
        Status = QuoteStatus.Accepted;
        UpdatedAt = updatedAt;
        Raise(new QuoteAcceptedDomainEvent(Id));
    }
    
    public void Reject(string reason, DateTime updatedAt)
    {
        if (Status != QuoteStatus.Pending)
            throw new DomainException("Only pending quotes can be rejected");
        
        Status = QuoteStatus.Rejected;
        UpdatedAt = updatedAt;
        Raise(new QuoteRejectedDomainEvent(Id, reason ?? string.Empty));
    }
    
    public void Expire(DateTime updatedAt)
    {
        if (Status == QuoteStatus.Pending && ValidUntil < updatedAt)
        {
            Status = QuoteStatus.Expired;
            UpdatedAt = updatedAt;
        }
    }

    /// <summary>
    /// LEAD-04: withdraws the offer because the negotiation behind it is over — the lead was
    /// marked Perdido.
    ///
    /// <para>
    /// Distinct from <see cref="Reject"/>, which only closes a PENDING quote: what actually
    /// hurt was the ACCEPTED one, because acceptance holds the vehicle Reservado. A deal that
    /// no longer exists kept a car off the floor and nothing ever noticed. Distinct from
    /// <see cref="Expire"/> too, which is a no-op before the validity date and would have
    /// silently done nothing.
    /// </para>
    ///
    /// <para>
    /// The record is kept, not deleted: the funnel has to be able to tell a lost negotiation
    /// from one that was never quoted. Idempotent — a quote already closed stays as it is, so
    /// an outbox retry changes nothing.
    /// </para>
    /// </summary>
    public void CloseAsLost(string reason, DateTime updatedAt)
    {
        if (Status is QuoteStatus.Rejected or QuoteStatus.Expired)
        {
            return;
        }

        Status = QuoteStatus.Rejected;
        UpdatedAt = updatedAt;
        Raise(new QuoteRejectedDomainEvent(Id, reason ?? string.Empty));
    }
    
    /// <summary>
    /// Attaches this quote to a client — what happens when its lead is converted.
    ///
    /// <para>
    /// The lead reference is deliberately KEPT. Clearing it used to be required by an
    /// "exactly one party" invariant, and it cost the deal its own history: converting a lead
    /// erased the only direct link back to the enquiry, so every downstream rule had to
    /// rediscover it through <c>Client.OriginLeadId</c> — and a lead whose quote was raised
    /// after conversion ended up with no quote it could see, stuck one stage behind with the
    /// board asking for a quote that already existed.
    /// </para>
    /// </summary>
    public void AssignClient(Guid clientId)
    {
        if (clientId == Guid.Empty)
            throw new DomainException("ClientId cannot be empty when assigning a quote to a client");

        ClientId = clientId;
    }

    /// <summary>
    /// Re-points this quote to a lead — the mirror of <see cref="AssignClient"/>. Used by the
    /// inquiry-clients backfill, which rebuilds the lead that a web enquiry should have created
    /// in the first place and moves that enquiry's quote onto it. Clears the client reference to
    /// keep the deal's history intact — the client reference, if any, is kept for the same
    /// reason <see cref="AssignClient"/> keeps the lead.
    /// </summary>
    public void AssignLead(Guid leadId)
    {
        if (leadId == Guid.Empty)
            throw new DomainException("LeadId cannot be empty when assigning a quote to a lead");

        LeadId = leadId;
        Lead = null;
    }

    /// <summary>
    /// Logically deletes the quote. The row is retained and excluded from default queries via
    /// the EF Core global query filter; this is an idempotent operation.
    /// </summary>
    public void Delete(DateTime deletedAtUtc)
    {
        if (IsDeleted)
            return;

        IsDeleted = true;
        DeletedAtUtc = deletedAtUtc;
    }
}