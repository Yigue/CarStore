using System.Diagnostics;
using Application.Abstractions.Auditing;

namespace Infrastructure.Auditing;

internal sealed class AuditContext : IAuditContext
{
    private int _systemDepth;

    public string? Reason { get; private set; }

    public string CorrelationId { get; } =
        Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

    public bool IsSystem => _systemDepth > 0;

    public void SetReason(string? reason)
    {
        if (reason is null)
        {
            Reason = null;
            return;
        }

        string trimmed = reason.Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("The audit reason cannot be empty.", nameof(reason));
        if (trimmed.Length > IAuditContext.MaxReasonLength)
        {
            throw new ArgumentException(
                $"The audit reason cannot exceed {IAuditContext.MaxReasonLength} characters.", nameof(reason));
        }

        Reason = trimmed;
    }

    public void ClearReason() => Reason = null;

    public IDisposable AsSystem()
    {
        _systemDepth++;
        return new Scope(this);
    }

    private sealed class Scope(AuditContext owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            owner._systemDepth--;
        }
    }
}
