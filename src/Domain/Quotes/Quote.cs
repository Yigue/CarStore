using SharedKernel;
using Domain.Cars;
using Domain.Clients;
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
    public Money ProposedPrice { get; private set; }
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
        DateTime date)
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

        
        ProposedPrice = new Money(proposedPrice);
        PaymentMethod = paymentMethod;
        ValidUntil = validUntil;
        Comments = comments ?? string.Empty;
        Status = QuoteStatus.Pending;
        CreatedAt = date;
        UpdatedAt = date;

        Raise(new QuoteCreatedDomainEvent(Id, CarId, ClientId ?? Guid.Empty, ProposedPrice));
    }

    public void Update(
        decimal proposedPrice,
        DateTime validUntil,
        string comments,
        DateTime updatedAt)
    {
        if (Status != QuoteStatus.Pending)
            throw new DomainException("Only pending quotes can be updated");
        
        if (validUntil <= updatedAt)
            throw new DomainException("ValidUntil must be in the future");
        
        ProposedPrice = new Money(proposedPrice);
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