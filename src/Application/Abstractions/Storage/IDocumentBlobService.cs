using Domain.Documents;

namespace Application.Abstractions.Storage;

public interface IDocumentBlobService
{
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken ct);
    Task<string> DownloadAsBase64Async(string blobUrl, CancellationToken ct);
    Task<Uri> GetSasUriAsync(string blobUrl, TimeSpan validFor);
}

public interface IDocumentOcrService
{
    Task<OcrExtractedData> AnalyzeDniAsync(string base64Content, CancellationToken ct);
    Task<OcrExtractedData> AnalyzeTituloAsync(string base64Content, CancellationToken ct);
}