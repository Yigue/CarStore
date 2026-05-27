using System;
using MediatR;
using SharedKernel;

namespace Application.Documents.Queries.GetDocumentDownloadUrl;

public sealed record GetDocumentDownloadUrlQuery(Guid DocumentId) : IRequest<Result<string>>;
