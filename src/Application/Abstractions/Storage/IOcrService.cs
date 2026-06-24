using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Application.Documents.DTOs;

namespace Application.Abstractions.Storage;

public interface IOcrService
{
    Task<ParsedDocumentDto> ParseAsync(Stream fileStream, string contentType, CancellationToken ct);
}
