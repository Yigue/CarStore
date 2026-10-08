namespace Domain.Sales.Attributes;

public enum SaleStatus
{
    Pending,
    Completed,
    Cancelled,
    // ADR C3: a Completed sale reverted with a mandatory reason (Sale.Void). Persisted as string.
    Voided
}
