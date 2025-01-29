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
}

