using System;
using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Platform.AuditLogs.GetPlatformAuditLogs;

public sealed record GetPlatformAuditLogsQuery(
    int Page = 1,
    int PageSize = 25,
    Guid? DealerId = null,
    string? Action = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null) : IQuery<PaginatedResult<PlatformAuditLogResponse>>;
