namespace Application.Abstractions.Auditing;

/// <summary>
/// sfc B2.2 / ADR C10: per-request (scoped) carrier of the business reason and correlation id the dealer audit log
/// stamps on every entry written by <c>TenantAuditInterceptor</c>. A handler that knows WHY it changes an audited
/// property (e.g. <c>Sale.Void</c>) calls <see cref="SetReason"/> before <c>SaveChangesAsync</c>. The reason belongs to
/// ONE save: the interceptor calls <see cref="ClearReason"/> once that save finishes (or fails).
/// </summary>
public interface IAuditContext
{
    /// <summary>Longest reason accepted (the audit reason column size).</summary>
    const int MaxReasonLength = 500;

    /// <summary>The reason set for the next save, or null.</summary>
    string? Reason { get; }

    /// <summary>Id shared by every entry written in this scope (the request trace id when there is one).</summary>
    string CorrelationId { get; }

    /// <summary>True while an explicit <see cref="AsSystem"/> scope is open.</summary>
    bool IsSystem { get; }

    /// <summary>
    /// Sets the reason for the next save. <c>null</c> clears it. The text is trimmed; empty/whitespace or longer than
    /// <see cref="MaxReasonLength"/> (after trimming) is rejected with <see cref="ArgumentException"/>.
    /// </summary>
    void SetReason(string? reason);

    void ClearReason();

    /// <summary>
    /// Marks the writes of the returned scope as an explicit system path (a job, a webhook): with no signed-in user the
    /// audit records the system actor. Without it, an audited write with no user context fails.
    /// </summary>
    IDisposable AsSystem();
}
