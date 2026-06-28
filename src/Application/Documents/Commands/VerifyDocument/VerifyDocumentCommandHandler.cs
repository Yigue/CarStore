using Application.Abstractions.Data;
using Application.Abstractions.Storage;
using Domain.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Documents.Commands.VerifyDocument;

internal sealed class VerifyDocumentCommandHandler : IRequestHandler<VerifyDocumentCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public VerifyDocumentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(VerifyDocumentCommand request, CancellationToken ct)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, ct);

        if (document is null)
        {
            return Result.Failure(new Error("Document.NotFound", "No se encontró el documento.", ErrorType.NotFound));
        }

        document.MarkAsProcessing();
        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }
}