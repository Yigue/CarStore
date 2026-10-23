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

    /// <summary>
    /// Quote proposal PDF (sfc B4.3 / B10 / RN-018, <see cref="QuoteProposalDocument"/>),
    /// rendered for <c>POST quotes/{id}/send</c>. This is this codebase's whole "PDF attached"
    /// story: there is no MIME-attachment capability anywhere in the email transport, so the
    /// rendered PDF is uploaded and its presigned URL is passed to the email template instead
    /// (see <c>SendQuoteCommandHandler</c>).
    /// </summary>
    QuoteProposal,
}
