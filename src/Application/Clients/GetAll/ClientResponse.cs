using Domain.Clients.Attributes;

namespace Application.Clients.GetAll;

public sealed record ClientResponse(
    Guid Id,
    Guid TenantId,
    Domain.Clients.Attributes.ClientType Type,
    ClientStatus Status,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string Phone,
    string? DocumentType,
    string DocumentNumber,
    string? City,
    string Address,
    string? ZipCode,
    string? Notes,
    string? AcquisitionSource,
    Guid? AssignedAgentId,
    decimal TotalSalesAmount,
    IReadOnlyList<PurchaseHistoryEntry> PurchaseHistory,
    DateTime? LastPurchaseDate,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsDeleted);

public sealed record PurchaseHistoryEntry(Guid SaleId, DateTime Date, decimal Amount);
