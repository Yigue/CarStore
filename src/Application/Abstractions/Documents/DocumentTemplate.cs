namespace Application.Abstractions.Documents;

/// <summary>
/// Named document templates renderable via <see cref="IDocumentRenderer"/>.
/// sfc B3.1 / B8: starts with <see cref="SaleContract"/> only (proves the rendering pipe
/// end-to-end). Later PRs (B3.2+) add more members without redesigning this contract.
/// </summary>
public enum DocumentTemplate
{
    /// <summary>"Boleto de compraventa" — full legal content owned by a later PR (B3.2).</summary>
    SaleContract,
}
