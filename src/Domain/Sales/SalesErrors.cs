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

    // ─── VEN-03 · payment terms ───────────────────────────────────────────────────────────────

    public static Error NegativePaymentAmount() => Error.Validation(
        "Sales.NegativePaymentAmount",
        "Down payment, trade-in value, financed amount and installment amount cannot be negative.");

    public static Error TradeInValueWithoutVehicle() => Error.Validation(
        "Sales.TradeInValueWithoutVehicle",
        "A trade-in value requires the vehicle being taken in part payment.");

    public static Error FinancingWithoutInstallments() => Error.Validation(
        "Sales.FinancingWithoutInstallments",
        "A financed amount requires a positive number of installments.");

    /// <summary>
    /// The parts of the payment do not add up to the price. Not a rounding complaint: a sale whose
    /// seña + permuta + financiado differs from its total is a number nobody can collect against,
    /// and the discrepancy would otherwise surface months later in the cobranza.
    /// </summary>
    public static Error PaymentPartsDoNotMatchPrice(decimal finalPrice, decimal parts) => Error.Validation(
        "Sales.PaymentPartsDoNotMatchPrice",
        $"The payment breakdown adds up to {parts:0.##} but the sale price is {finalPrice:0.##}.");

    /// <summary>
    /// A sale with no price is not a sale. Normally caught by the validator; this exists so the
    /// aggregate is never handed a price it cannot honour.
    /// </summary>
    public static Error PriceRequired() => Error.Validation(
        "Sales.PriceRequired",
        "The sale needs a final price and a payment method, either supplied or inherited from an accepted quote.");

    // ─── VEN-02 · selling to a client that is still a prospect ────────────────────────────────

    /// <summary>
    /// A Prospect is a person the CRM created automatically when the lead reached Negociación:
    /// a name, an email and a placeholder DNI. That is enough to quote and not enough to invoice.
    /// </summary>
    public static Error ClientDataIncomplete(Guid clientId) => Error.Problem(
        "Sales.ClientDataIncomplete",
        $"The client with Id = '{clientId}' is still a prospect with incomplete data. DNI and address are required before invoicing.");
}

