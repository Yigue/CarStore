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
}

