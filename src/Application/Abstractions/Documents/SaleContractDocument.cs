namespace Application.Abstractions.Documents;

/// <summary>
/// Minimal data needed to prove the <see cref="DocumentTemplate.SaleContract"/> rendering
/// pipe works end-to-end (sfc B3.1 / B8). Intentionally small: a later PR (B3.2) owns the
/// full legal content of the "boleto de compraventa" (built from <c>Sale</c>, <c>Client</c>,
/// <c>Car</c> and <c>DealerSettings</c>) and will extend or replace this model as needed.
/// </summary>
public sealed record SaleContractDocument(string ContractNumber, string LicensePlate);
