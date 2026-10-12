namespace Application.Abstractions.Auditing;

/// <summary>
/// sfc B2.2 / ADR C10: per-request (scoped) carrier of the business reason and correlation id the dealer audit log
/// stamps on every entry written by <c>TenantAuditInterceptor</c>. A handler that knows WHY it changes an audited
/// property (e.g. <c>Sale.Void</c>) calls <see cref="SetReason"/> before <c>SaveChangesAsync</c>.
/// </summary>
public interface IAuditContext
{
    /// <summary>The reason set for the current unit of work, or null.</summary>
    string? Reason { get; }

    /// <summary>Id shared by every entry written in this scope (the request trace id when there is one).</summary>
    string CorrelationId { get; }

    void SetReason(string? reason);
}
