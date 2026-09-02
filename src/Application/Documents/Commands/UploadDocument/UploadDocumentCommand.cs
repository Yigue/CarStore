using Application.Abstractions.Messaging;

namespace Application.Documents.Commands.UploadDocument;

public sealed record UploadDocumentCommand(
    Guid ClientId,
    Domain.Documents.DocumentType Type,
    string Base64Content,
    string FileName,
    string ContentType,
    /// <summary>
    /// The sale this paperwork belongs to, when it is uploaded while closing a deal. Optional:
    /// identity documents belong to the person regardless of any single purchase, while the
    /// contract and transfer form belong to one transaction and were previously indistinguishable
    /// from them.
    /// </summary>
    Guid? SaleId = null
) : ICommand<Guid>;