using Domain.Documents;

namespace Application.Documents.Dtos;

public sealed record DocumentDto(
    Guid Id,
    Guid ClientId,
    string Type,
    string Status,
    string BlobUrl,
    OcrExtractedDataDto? ExtractedData,
    string? DiscrepancyNotes,
    DateTime UploadedAtUtc
);

public sealed record OcrExtractedDataDto(
    string? FullName,
    string? DocumentNumber,
    string? IssueDate,
    string? VehicleTitleNumber,
    string? VehicleIdentifier
);