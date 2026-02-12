using SharedKernel;
using Domain.Cars;
using Domain.Clients;
using Domain.Quotes.Attributes;
using Domain.Quotes.Events;
using Domain.Shared.ValueObjects;

namespace Domain.Quotes;

public sealed class Quote : Entity
{
    public Guid CarId { get; private set; }
    public Guid ClientId { get; private set; }
    public Money ProposedPrice { get; private set; }
    public QuoteStatus Status { get; private set; }
    public DateTime ValidUntil { get; private set; }
    public string Comments { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public Car Car { get; private set; }
    public Client Client { get; private set; }

    private Quote() { }

    public Quote(
        Car car,
        Client client,
        decimal proposedPrice,
        DateTime validUntil,
        string comments,
        DateTime date)
    {
        if (validUntil <= date)
            throw new DomainException("ValidUntil must be in the future");
        
        Car = car;
        CarId = car.Id;
        Client = client;
        ClientId = client.Id;
        ProposedPrice = new Money(proposedPrice);
        ValidUntil = validUntil;
        Comments = comments ?? string.Empty;
        Status = QuoteStatus.Pending;
        CreatedAt = date;
        UpdatedAt = date;

        Raise(new QuoteCreatedDomainEvent(Id, CarId, ClientId, ProposedPrice));
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
}
