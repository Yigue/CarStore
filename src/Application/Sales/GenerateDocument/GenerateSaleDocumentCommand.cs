using Application.Abstractions.Messaging;

namespace Application.Sales.GenerateDocument;

/// <summary>
/// sfc B3.2 / B8 / RN-028: generates a legal PDF for a sale (boleto or Formulario 08 data
/// sheet) and registers it as a <c>Document</c> row attached to the sale. Regenerating never
/// overwrites a previous version — see <see cref="GenerateSaleDocumentCommandHandler"/>.
/// </summary>
public sealed record GenerateSaleDocumentCommand(
    Guid SaleId,
    SaleDocumentKind Kind) : ICommand<GenerateSaleDocumentResult>;
