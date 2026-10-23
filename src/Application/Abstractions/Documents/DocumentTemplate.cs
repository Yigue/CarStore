namespace Application.Abstractions.Documents;

/// <summary>
/// Named document templates renderable via <see cref="IDocumentRenderer"/>.
/// sfc B3.1 / B8 started with <see cref="SaleContract"/> only (proving the rendering pipe
/// end-to-end). sfc B3.2 / RN-028 adds <see cref="Formulario08DataSheet"/>.
/// </summary>
public enum DocumentTemplate
{
    /// <summary>"Boleto de compraventa" — full legal content, owned by sfc B3.2 (<see cref="SaleContractDocument"/>).</summary>
    SaleContract,

    /// <summary>
    /// Pre-filled reference sheet for Formulario 08 (sfc B3.2 / RN-028). NOT the official DNRPA
    /// form — that one carries security elements CarStore cannot reproduce. This is a plain
    /// data sheet with the same data, for the dealer's own use before presenting the real form.
    /// </summary>
    Formulario08DataSheet,
}
