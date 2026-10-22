-- sfc B4.3 pre-check (dry run, read-only). RN-018 / ADR C7.
-- Run against staging BEFORE applying the AddQuoteNumbers migration and review the report: how
-- many quotes each dealer already has (what dealer_sequences.next_value will end up seeded to),
-- and a preview of the exact folio each existing quote would receive from
-- ROW_NUMBER() OVER (PARTITION BY dealer_id ORDER BY created_at, id) — the identical ordering the
-- migration's UPDATE uses. Nothing here writes anything.
WITH numbered AS (
    SELECT q.id,
           q.dealer_id,
           q.created_at,
           q.is_deleted,
           ROW_NUMBER() OVER (PARTITION BY q.dealer_id ORDER BY q.created_at, q.id) AS seq
    FROM public.quotes q
    WHERE q.number IS NULL
)
SELECT dealer_id,
       count(*)                                                        AS quotes_to_number,
       count(*) FILTER (WHERE is_deleted)                              AS deleted_quotes_included,
       min(created_at)                                                 AS oldest_quote,
       max(created_at)                                                 AS newest_quote,
       max(seq) + 1                                                    AS dealer_sequences_next_value_after
FROM numbered
GROUP BY dealer_id
ORDER BY dealer_id;

-- Detail (uncomment for a full per-row preview of the exact folio before a manual review):
--
-- SELECT id, dealer_id, created_at,
--        'COT-' || to_char(created_at, 'YYYY') || '-' || lpad(seq::text, 6, '0') AS would_be_number
-- FROM numbered
-- ORDER BY dealer_id, seq;
