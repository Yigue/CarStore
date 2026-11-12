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
    
    public static Error AlreadyProcessed(Guid quoteId) => Error.Conflict(
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

    /// <summary>
    /// The same party already has a live offer on the same unit. Competing offers from
    /// DIFFERENT buyers are the normal case and stay allowed (see
    /// <see cref="CarAlreadyCommitted"/> for what actually locks a car); what this refuses is
    /// the same buyer being quoted the same unit twice while the first offer is still standing.
    ///
    /// <para>
    /// LEAD-02: the board had no path from a lead to an existing quote, so every reopening of
    /// the form — a double submit, or someone going back to "edit the amount" — wrote another
    /// row. The dealership then had two live prices for one negotiation and no way to say which
    /// one it had promised. Re-pricing is an update of the standing offer, not a second one.
    /// </para>
    /// </summary>
    public static Error ActiveQuoteAlreadyExists(Guid carId, Guid quoteId) => Error.Conflict(
        "Quotes.ActiveQuoteAlreadyExists",
        $"There is already a pending quote ('{quoteId}') for the car with Id = '{carId}' and this same party. Update that quote instead of creating a second one.");

    /// <summary>
    /// sfc B4.3 / B10 / RN-018: <c>POST quotes/{id}/send</c> sends to the CLIENT's email, not the
    /// lead's — see <c>SendQuoteCommandHandler</c>'s XML doc for why. Raised both when the quote
    /// has no Client at all (lead-only) and, defensively, if a Client's email were ever blank —
    /// provably unreachable today (see <c>SendQuoteCommandHandlerTests</c>'s class remarks), kept
    /// because the check costs nothing and documents the invariant this error actually names.
    /// <para>
    /// The capability spec (quote-numbering-pdf) describes this as a 422
    /// <c>MISSING_CONTACT_EMAIL</c> response; this codebase's <c>CustomResults.Problem</c> has no
    /// 422 mapping for any <see cref="ErrorType"/> (only 400/404/409/403/500 exist), so this uses
    /// <see cref="ErrorType.Validation"/> → 400, the same status every other validation gate in
    /// this codebase returns (e.g. RN-028's InvoiceNumberRequiredForDocument).
    /// </para>
    /// </summary>
    public static Error MissingContactEmail(Guid quoteId) => Error.Validation(
        "Quotes.MissingContactEmail",
        $"The quote with Id = '{quoteId}' cannot be sent because it has no client with an email on file.");

    /// <summary>
    /// sfc B4.3 / B10 / RN-018: a quote created before the AddQuoteNumbers backfill ran (or one
    /// whose backfill is still pending in this environment) has no folio yet — sending it would
    /// put an email token in the recipient's inbox with nothing a dealer could reconcile against.
    /// </summary>
    public static Error NotNumbered(Guid quoteId) => Error.Validation(
        "Quotes.NotNumbered",
        $"The quote with Id = '{quoteId}' must be assigned a folio number before it can be sent.");
}

