namespace Application.Quotes.Get;

public sealed class QuoteResponse
{
    public Guid Id { get; init; }
    public Guid CarId { get; init; }
    public Guid? ClientId { get; init; }
    public Guid? LeadId { get; init; }
    public decimal ProposedPrice { get; init; }
    public string PaymentMethod { get; init; }
    public string Status { get; init; }
    public DateTime ValidUntil { get; init; }
    public string Comments { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string CarBrand { get; init; }
    public string CarModel { get; init; }
    public string ClientName { get; init; }

    // REQ-QT-XREF-001: read-only cross-reference to the linked counterpart. Set when
    // the quote is Client-linked and that client came from a lead (OriginLeadId), or
    // when the quote is Lead-linked and that lead was converted (ConvertedClientId).
    // Projection only — the Quote single-party invariant is untouched.
    public Guid? OriginLeadId { get; init; }
    public Guid? ConvertedClientId { get; init; }
}
