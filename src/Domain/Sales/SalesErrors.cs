using SharedKernel;

namespace Domain.Sales;

public static class SalesErrors
{
    public static Error AlreadySold(Guid saleId) => Error.Problem(
        "Sales.AlreadySold",
        $"The sale with Id = '{saleId}' is already sold.");

    public static Error NotFound(Guid saleId) => Error.NotFound(
        "Sales.NotFound",
        $"The sale with the Id = '{saleId}' was not found");
    public static Error QuoteExpired(Guid saleId) => Error.NotFound(
        "Sales.QuoteExpired",
        $"The quote with the Id = '{saleId}' was not found");
    public static Error NotAllAtributes(Guid saleId) => Error.NotFound(
        "Sales.NotAllAttributes",
        $"The sale with the Id = '{saleId}' was not found");
    
    public static Error InvalidPrice() => Error.Problem(
        "Sales.InvalidPrice",
        "FinalPrice must be greater than 0");

    public static Error CannotEditNonPending(Guid saleId) => Error.Conflict(
        "Sales.CannotEditNonPending",
        $"The sale with Id = '{saleId}' cannot be edited because it is not in a pending state.");

    public static Error AlreadyConvertedFromQuote(Guid quoteId) => Error.Conflict(
        "Sales.AlreadyConvertedFromQuote",
        $"The quote with Id = '{quoteId}' has already been converted into a sale.");

    public static Error CannotDeleteCompleted(Guid saleId) => Error.Conflict(
        "Sales.CannotDeleteCompleted",
        $"The sale with Id = '{saleId}' cannot be deleted because it is Completed. Completed sales have a linked financial transaction.");

    public static Error QuoteNotFound(Guid quoteId) => Error.NotFound(
        "Sales.QuoteNotFound",
        $"The quote with Id = '{quoteId}' referenced by this sale was not found.");

    public static Error QuoteNotAccepted(Guid quoteId) => Error.Problem(
        "Sales.QuoteNotAccepted",
        $"The quote with Id = '{quoteId}' cannot be converted into a sale because it is not Accepted.");

    public static Error QuoteMismatch(Guid quoteId) => Error.Problem(
        "Sales.QuoteMismatch",
        $"The car or client of this sale does not match the accepted quote with Id = '{quoteId}'.");
}

