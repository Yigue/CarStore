namespace Application.Documents.DTOs;

public record ParsedDocumentDto(
    string? DocumentType,
    string? DocumentNumber,
    string? FirstName,
    string? LastName,
    string? LicensePlate,
    string? ChassisNumber,
    string? Year
);
