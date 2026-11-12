namespace Application.Quotes.Send;

/// <summary>sfc B4.3 / B10 / RN-018: the generated proposal document and the URL emailed to the client.</summary>
public sealed record SendQuoteResult(Guid DocumentId, string QuoteUrl);
