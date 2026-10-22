using Application.Abstractions.Data;
using Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Common;

/// <summary>
/// sfc B4.3 / RN-018 / ADR C7: atomic per-dealer sequence allocator.
///
/// <para>
/// Relational providers (Postgres, SQLite): one round trip, a single
/// <c>INSERT ... ON CONFLICT (dealer_id, kind) DO UPDATE ... RETURNING</c> statement. There is no
/// separate "ensure the row exists" step — that would itself race on a brand-new (dealer, kind)
/// pair. Both branches converge on the same RETURNING expression (<c>next_value - 1</c>): the
/// insert branch seeds <c>next_value = 2</c> and returns 1; the conflict branch increments the
/// existing row by 1 and returns the pre-increment value. Parameters go through
/// <c>Database.SqlQueryRaw</c> (not hand-built ADO parameters) so EF's own provider-specific type
/// mapping for <see cref="Guid"/> is reused — the same mapping the column itself was created
/// with — instead of guessing at Postgres uuid vs. SQLite TEXT storage.
/// </para>
/// <para>
/// InMemory provider (ApplicationTests): no raw SQL / RETURNING support, so this falls back to a
/// plain read-increment-write on the <see cref="DealerSequence"/> row via its own non-atomic
/// domain method. The InMemory provider never runs these calls concurrently in tests — only the
/// real relational path (tests/InfrastructureEfTests/Quotes/DealerSequenceConcurrencyTests.cs)
/// has to prove duplicate-free allocation under concurrency.
/// </para>
/// </summary>
internal sealed class DealerSequenceAllocator(IApplicationDbContext context) : IDealerSequenceAllocator
{
    private const string AllocateSql = """
        INSERT INTO dealer_sequences (id, dealer_id, kind, next_value)
        VALUES ({0}, {1}, {2}, 2)
        ON CONFLICT (dealer_id, kind) DO UPDATE SET next_value = dealer_sequences.next_value + 1
        RETURNING next_value - 1
        """;

    public async Task<long> NextAsync(Guid dealerId, string kind, CancellationToken cancellationToken = default)
    {
        if (context is not DbContext dbContext)
        {
            throw new InvalidOperationException("DealerSequenceAllocator requires an EF Core DbContext.");
        }

        if (!dbContext.Database.IsRelational())
        {
            return await NextNonAtomicAsync(context, dealerId, kind, cancellationToken);
        }

        List<long> rows = await dbContext.Database
            .SqlQueryRaw<long>(AllocateSql, Guid.NewGuid(), dealerId, kind)
            .ToListAsync(cancellationToken);

        return rows[0];
    }

    private static async Task<long> NextNonAtomicAsync(
        IApplicationDbContext context, Guid dealerId, string kind, CancellationToken cancellationToken)
    {
        DealerSequence? sequence = await context.DealerSequences
            .FirstOrDefaultAsync(s => s.DealerId == dealerId && s.Kind == kind, cancellationToken);

        if (sequence is null)
        {
            sequence = DealerSequence.Create(dealerId, kind);
            context.DealerSequences.Add(sequence);
        }

        long allocated = sequence.AllocateNonAtomic();
        await context.SaveChangesAsync(cancellationToken);
        return allocated;
    }
}
