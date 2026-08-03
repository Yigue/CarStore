using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WebApiTests.Postgres;

/// <summary>
/// PR4 Slice 12 regression guard (D1, REQ: postgres-collation-provisioning).
/// Captures the SQL text of every command executed against the DbContext it is attached to,
/// so tests can assert that filtering/pagination/collation are pushed down to the database
/// instead of happening in application memory after a full-table load.
/// </summary>
public sealed class SqlCommandCapturingInterceptor : DbCommandInterceptor
{
    private readonly ConcurrentBag<string> _commandTexts = new();

    public IReadOnlyCollection<string> CommandTexts => _commandTexts.ToArray();

    public void Clear() => _commandTexts.Clear();

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        _commandTexts.Add(command.CommandText);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        _commandTexts.Add(command.CommandText);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
