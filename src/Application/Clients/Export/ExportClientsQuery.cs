using Application.Abstractions.Messaging;

namespace Application.Clients.Export;

/// <summary>
/// Query to export clients as a CSV byte array. Supports optional id-list filter and
/// the same search/filter params as GetAllClientsQuery. Returns 413 if &gt;10 000 rows.
/// </summary>
public sealed record ExportClientsQuery(
    IReadOnlyList<Guid>? Ids = null,
    string? Search = null,
    string? Status = null,
    string? Type = null,
    string? Source = null,
    Guid? AssignedAgentId = null,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null) : IQuery<byte[]>;
