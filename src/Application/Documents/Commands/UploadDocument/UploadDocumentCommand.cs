using Application.Abstractions.Messaging;

namespace Application.Documents.Commands.UploadDocument;

public sealed record UploadDocumentCommand(
    Guid ClientId,
    Domain.Documents.DocumentType Type,
    string Base64Content,
    string FileName,
    string ContentType
) : ICommand<Guid>;