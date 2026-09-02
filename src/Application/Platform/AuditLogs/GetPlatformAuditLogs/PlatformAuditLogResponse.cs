using System;

namespace Application.Platform.AuditLogs.GetPlatformAuditLogs;

public sealed record PlatformAuditLogResponse(
    Guid Id,
    Guid DealerId,
    Guid DealerSettingsId,
    string DealerName,
    string Action,
    Guid ActorUserId,
    string? ActorEmail,
    string ActorKind,
    DateTime OccurredAtUtc,
    DateTime RecordedAtUtc,
    string? Reason);
