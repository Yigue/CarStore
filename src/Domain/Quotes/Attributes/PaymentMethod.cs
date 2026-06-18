namespace Domain.Quotes.Attributes;

/// <summary>
/// Intended payment arrangement for a vehicle quote. Quote-context specific and
/// intentionally separate from <c>Domain.Financial.Attributes.PaymentMethod</c>
/// (which models how money actually moved in a finance transaction). A quote
/// expresses buyer intent for a car deal, not a settled cash-flow instrument.
/// </summary>
public enum PaymentMethod
{
    Contado,
    Financiado,
    Permuta,
    Mixto
}
