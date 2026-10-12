using System.Diagnostics;
using Application.Abstractions.Auditing;

namespace Infrastructure.Auditing;

internal sealed class AuditContext : IAuditContext
{
    public string? Reason { get; private set; }

    public string CorrelationId { get; } =
        Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

    public void SetReason(string? reason) => Reason = reason;
}
