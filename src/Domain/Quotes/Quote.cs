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

        if (client is null && lead is null)
            throw new DomainException("A quote must have either a Client or a Lead");
        if (client is not null && lead is not null)
            throw new DomainException("A quote cannot have both a Client and a Lead");

        if (validUntil <= date)
            throw new DomainException("ValidUntil must be in the future");

        Car = car;
        CarId = car.Id;
        
        if (client is not null)
        {
            Client = client;
            ClientId = client.Id;
        }
        else
        {
            Lead = lead;
            LeadId = lead!.Id;
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
    /// Re-points this quote to a client (e.g. when its lead is converted). Enforces the
    /// "exactly one party" invariant by clearing the lead reference so the quote does not
    /// end up owned by both a lead and a client.
    /// </summary>
    public void AssignClient(Guid clientId)
    {
        if (clientId == Guid.Empty)
            throw new DomainException("ClientId cannot be empty when assigning a quote to a client");

        ClientId = clientId;
        LeadId = null;
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