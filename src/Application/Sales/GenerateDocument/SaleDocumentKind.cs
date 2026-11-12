namespace Application.Sales.GenerateDocument;

/// <summary>sfc B3.2 / B8 / RN-028: which legal document to generate for a sale.</summary>
public enum SaleDocumentKind
{
    /// <summary>"Boleto de compraventa" — full legal contract.</summary>
    Boleto,

    /// <summary>Pre-filled data sheet for Formulario 08 (not the official DNRPA form).</summary>
    Formulario08,
}
