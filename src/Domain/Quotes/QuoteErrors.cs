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
}

