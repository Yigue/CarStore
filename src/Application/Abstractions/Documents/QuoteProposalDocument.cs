namespace Application.Abstractions.Documents;

/// <summary>
/// Content of the quote proposal PDF (sfc B4.3 / B10 / RN-018), built from <c>Quote</c>,
/// <c>Car</c>, the quote's <c>Client</c> and <c>DealerSettings</c>.
/// <para>
/// Narrower than <see cref="SaleContractDocument"/> on purpose: a quote is an offer, not a
/// signed deal, so it carries the car, the price and its validity window rather than legal
/// transfer paperwork.
/// </para>
/// </summary>
public sealed record QuoteProposalDocument(
    string QuoteNumber,
    string CarBrand,
    string CarModel,
    int CarYear,
    decimal ProposedPrice,
    string Currency,
    string PaymentMethod,
    DateTime ValidUntil,
    string ClientFullName,
    string? Comments,
    string DealerName,
    string? DealerAddress,
    string? DealerPhoneNumber);
