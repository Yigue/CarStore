using SharedKernel;

namespace Domain.Quotes;

public static class QuoteErrors
{
    public static Error AlreadySold(Guid quoteId) => Error.Problem(
        "Quotes.AlreadySold",
        $"The quote with Id = '{quoteId}' is already sold.");

    public static Error NotFound(Guid quoteId) => Error.NotFound(
        "Quotes.NotFound",
        $"The quote with the Id = '{quoteId}' was not found");
    public static Error NotAllAtributes(Guid quoteId) => Error.NotFound(
        "Quotes.NotAllAttributes",
        $"The quote with the Id = '{quoteId}' was not found");
    
    public static Error Expired(Guid quoteId) => Error.Problem(
        "Quotes.Expired",
        $"The quote with Id = '{quoteId}' has expired");
    
    public static Error InvalidValidUntil() => Error.Problem(
        "Quotes.InvalidValidUntil",
        "The ValidUntil date must be in the future");
    
    public static Error AlreadyProcessed(Guid quoteId) => Error.Problem(
        "Quotes.AlreadyProcessed",
        $"The quote with Id = '{quoteId}' has already been processed (accepted or rejected)");
    
    public static Error CannotDeleteNonPendingQuote(Guid quoteId) => Error.Problem(
        "Quotes.CannotDeleteNonPendingQuote",
        $"The quote with Id = '{quoteId}' cannot be deleted because it is not in Pending status");

    public static Error ClientNotQuotable(Guid clientId) => Error.Problem(
        "Quotes.ClientNotQuotable",
        $"The client with Id = '{clientId}' cannot be quoted because it is marked as Lost.");

    public static Error LeadNotQuotable(Guid leadId) => Error.Problem(
        "Quotes.LeadNotQuotable",
        $"The lead with Id = '{leadId}' cannot be quoted because it is Perdido or Archivado.");

    /// <summary>
    /// A car can carry as many competing offers as the market brings, but only one of them can
    /// be accepted: acceptance is the moment the dealership commits the unit to a buyer.
    /// Raised both when a second acceptance is attempted and when a new quote is raised for a
    /// car that is already committed.
    /// </summary>
    public static Error CarAlreadyCommitted(Guid carId) => Error.Conflict(
        "Quotes.CarAlreadyCommitted",
        $"The car with Id = '{carId}' already has an accepted quote. Reject or expire it before committing the car again.");
}

