using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Auditing.GetAuditLog;

/// <summary>sfc B2.2.4 / A20: the dealer's own audit trail, newest first. <paramref name="Entity"/> is the CLR name (<c>Car</c>, <c>Sale</c>).</summary>
public sealed record GetAuditLogQuery(
    string? Entity = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 25) : IQuery<PaginatedResult<DealerAuditLogResponse>>;

public sealed record DealerAuditLogResponse(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Property,
    string? OldValue,
    string? NewValue,
    Guid ActorUserId,
    string? Reason,
    string CorrelationId,
    DateTime OccurredAtUtc);
