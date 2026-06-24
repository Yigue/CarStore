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
}

