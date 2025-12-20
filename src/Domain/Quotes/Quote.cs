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
        string comments)
    {
        if (validUntil <= DateTime.UtcNow)
            throw new DomainException("ValidUntil must be in the future");
        
        Car = car;
        CarId = car.Id;
        Client = client;
        ClientId = client.Id;
        ProposedPrice = new Money(proposedPrice);
        ValidUntil = validUntil;
        Comments = comments ?? string.Empty;
        Status = QuoteStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        Raise(new QuoteCreatedDomainEvent(Id, CarId, ClientId, ProposedPrice.Amount));
    }

    public void Update(
        decimal proposedPrice,
        DateTime validUntil,
        string comments)
    {
        if (Status != QuoteStatus.Pending)
            throw new DomainException("Only pending quotes can be updated");
        
        if (validUntil <= DateTime.UtcNow)
            throw new DomainException("ValidUntil must be in the future");
        
        ProposedPrice = new Money(proposedPrice);
        ValidUntil = validUntil;
        Comments = comments ?? string.Empty;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void Update(
        Money proposedPrice,
        DateTime validUntil,
        string comments)
    {
        if (Status != QuoteStatus.Pending)
            throw new DomainException("Only pending quotes can be updated");
        
        if (validUntil <= DateTime.UtcNow)
            throw new DomainException("ValidUntil must be in the future");
        
        ProposedPrice = proposedPrice;
        ValidUntil = validUntil;
        Comments = comments ?? string.Empty;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void Accept()
    {
        if (Status != QuoteStatus.Pending)
            throw new DomainException("Only pending quotes can be accepted");
        
        if (ValidUntil < DateTime.UtcNow)
            throw new DomainException("Cannot accept an expired quote");
        
        Status = QuoteStatus.Accepted;
        UpdatedAt = DateTime.UtcNow;
        Raise(new QuoteAcceptedDomainEvent(Id));
    }
    
    public void Reject(string reason)
    {
        if (Status != QuoteStatus.Pending)
            throw new DomainException("Only pending quotes can be rejected");
        
        Status = QuoteStatus.Rejected;
        UpdatedAt = DateTime.UtcNow;
        Raise(new QuoteRejectedDomainEvent(Id, reason ?? string.Empty));
    }
    
    public void Expire()
    {
        if (Status == QuoteStatus.Pending && ValidUntil < DateTime.UtcNow)
        {
            Status = QuoteStatus.Expired;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
