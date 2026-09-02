using Application.Abstractions.Messaging;
using Application.Documents.Dtos;

namespace Application.Documents.Queries.GetSaleDocuments;

/// <summary>
/// The paperwork attached to one sale — the contract, the transfer form, the invoice. Distinct
/// from the client's own documents, which belong to the person regardless of any single purchase.
/// </summary>
public sealed record GetSaleDocumentsQuery(Guid SaleId) : IQuery<IReadOnlyList<DocumentDto>>;
