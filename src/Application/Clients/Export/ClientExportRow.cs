namespace Application.Clients.Export;

/// <summary>
/// Flat projection used when streaming clients to CSV. Only includes fields relevant for export.
/// </summary>
public sealed record ClientExportRow(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string? DocumentNumber,
    string? City,
    string Address,
    string Status,
    string Type,
    string? AcquisitionSource,
    decimal TotalSalesAmount,
    DateTime CreatedAt);
