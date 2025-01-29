using SharedKernel;
using Domain.Cars;
using Domain.Clients;
using Domain.Quotes.Attributes;
using Domain.Quotes.Events;

namespace Domain.Quotes;

public sealed class Quote : Entity
{
    public Guid CarId { get; set; }
    public Car Car { get; set; }
    public Guid ClientId { get; set; }
    public Client Client { get; set; }
    public decimal ProposedPrice { get; set; }
    public QuoteStatus Status { get; set; }
    public DateTime ValidUntil { get; set; }
    public string Comments { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    private Quote() { }

    public Quote(
        Car car,
        Client client,
        decimal proposedPrice,
        DateTime validUntil,
        string comments)
    {
        Car = car;
        CarId = car.Id;
        Client = client;
        ClientId = client.Id;
        ProposedPrice = proposedPrice;
        ValidUntil = validUntil;
        Comments = comments;
        Status = QuoteStatus.Pending;
        CreatedAt = DateTime.UtcNow;

        // Raise(new QuoteCreatedDomainEvent(Id, CarId, ClientId, ProposedPrice));
    }

    public void Update(
        decimal proposedPrice,
        DateTime validUntil,
        QuoteStatus status,
        string comments)
    {
        ProposedPrice = proposedPrice;
        ValidUntil = validUntil;
        Status = status;
        Comments = comments;
        UpdatedAt = DateTime.UtcNow;
    }
}
