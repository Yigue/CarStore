namespace Application.Quotes.Get;

public sealed class QuoteResponse
{
    public Guid Id { get; init; }
    public Guid CarId { get; init; }
    public Guid ClientId { get; init; }
    public decimal ProposedPrice { get; init; }
    public string Status { get; init; }
    public DateTime ValidUntil { get; init; }
    public string Comments { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string CarBrand { get; init; }
    public string CarModel { get; init; }
    public string ClientName { get; init; }
}
