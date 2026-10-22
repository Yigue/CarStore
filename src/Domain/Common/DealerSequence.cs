using SharedKernel;

namespace Domain.Common;

/// <summary>
/// sfc B4.3 / RN-018 / ADR C7: one monotonic counter per (dealer, kind) — e.g. "quote" folios
/// (<c>COT-{yyyy}-{n:D6}</c>). This entity only models the row; atomic allocation under real
/// concurrency is an infrastructure concern (see IDealerSequenceAllocator /
/// Infrastructure.Common.DealerSequenceAllocator), backed by the unique (dealer_id, kind) index
/// <c>ux_dealer_sequences_dealer_id_kind</c> so two dealers/kinds never collide and the DB
/// serializes concurrent increments landing on the same row.
/// </summary>
public sealed class DealerSequence : Entity
{
    public string Kind { get; private set; } = string.Empty;
    public long NextValue { get; private set; }

    private DealerSequence() { }

    public static DealerSequence Create(Guid dealerId, string kind, long startAt = 1)
    {
        if (string.IsNullOrWhiteSpace(kind))
            throw new DomainException("Sequence kind cannot be empty");

        if (startAt < 1)
            throw new DomainException("Sequence must start at 1 or above");

        var sequence = new DealerSequence { Kind = kind, NextValue = startAt };
        sequence.SetDealer(dealerId);
        return sequence;
    }

    /// <summary>
    /// Non-atomic increment. Used ONLY by DealerSequenceAllocator's InMemory-provider fallback
    /// (ApplicationTests has no raw SQL / RETURNING support). The real path — relational
    /// providers, exercised in tests/InfrastructureEfTests/Quotes/DealerSequenceConcurrencyTests.cs
    /// — never calls this: it allocates via a single atomic upsert-and-RETURNING SQL statement.
    /// </summary>
    public long AllocateNonAtomic()
    {
        long allocated = NextValue;
        NextValue++;
        return allocated;
    }

    /// <summary>Used only by the AddQuoteNumbers migration backfill to seed next_value from existing data.</summary>
    public void SetNextValue(long nextValue)
    {
        if (nextValue < 1)
            throw new DomainException("Sequence NextValue must be 1 or above");

        NextValue = nextValue;
    }
}
