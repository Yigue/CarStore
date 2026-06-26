-- scripts/round-transaction-amounts.sql
-- REQ-FIN-PRECISION-001: pre-deploy rounding step for AddMoneyPrecision.
--
-- Run BEFORE `dotnet ef database update AddMoneyPrecision` against the
-- production replica leader during a low-traffic window.
--
-- Idempotent: only rounds rows whose amount currently has more than 2
-- decimal places. Safe to re-run after the migration has widened the column.
--
-- Postgres ROUND(numeric, integer) uses HALF_AWAY_FROM_ZERO rounding; the EF
-- layer's `decimal` precision won't change the result.

BEGIN;

UPDATE transactions
SET amount = ROUND(amount, 2)
WHERE amount <> ROUND(amount, 2);

COMMIT;

-- Diagnostic (read-only; safe to run any time):
-- SELECT COUNT(*) AS rows_with_more_than_2dp
-- FROM transactions
-- WHERE amount <> ROUND(amount, 2);
-- Expect 0 after a successful run.