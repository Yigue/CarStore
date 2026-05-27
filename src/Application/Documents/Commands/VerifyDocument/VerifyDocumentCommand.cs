using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Documents.Commands.VerifyDocument;

public sealed record VerifyDocumentCommand(Guid DocumentId) : ICommand;