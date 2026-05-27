using Application.Abstractions.Messaging;
using Application.Documents.Dtos;

namespace Application.Documents.Queries.GetClientDocuments;

public sealed record GetClientDocumentsQuery(Guid ClientId) : IQuery<IReadOnlyList<DocumentDto>>;