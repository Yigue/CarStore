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
    /// <summary>
    /// The lead this client was converted from, when there was one. Stored on Client since the
    /// CRM funnel existed and exposed nowhere, so the detail screen could not link back to where
    /// the customer actually came from.
    /// </summary>
    Guid? OriginLeadId,
    Guid? AssignedAgentId,
    decimal TotalSalesAmount,
    IReadOnlyList<PurchaseHistoryEntry> PurchaseHistory,
    DateTime? LastPurchaseDate,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsDeleted,
    // sfc C2.4 / RN-047: opt-in to after-sales service reminders.
    bool AcceptsServiceReminders = false);

public sealed record PurchaseHistoryEntry(Guid SaleId, DateTime Date, decimal Amount);
