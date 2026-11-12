-- sfc B0.1.5 — pre-check de la migración 20261010152035_AddMoneyCurrencyColumns (solo lectura).
--
-- Uso (staging o producción, ANTES y DESPUÉS de aplicar la migración):
--   psql "$DATABASE_URL" -f scripts/check-money-currency-precheck.sql > precheck-<antes|despues>.txt
--   diff precheck-antes.txt precheck-despues.txt
--
-- ANTES de la migración las columnas *_currency no existen: la sección 2 falla con "column does not exist"
-- (esperado, ignorarla). La sección 1 tiene que dar exactamente los mismos números antes y después.

\echo '== 1. Conteos (no deben cambiar con la migración) =='
SELECT 'cars'         AS tabla, count(*) AS filas, count(*) FILTER (WHERE is_deleted) AS eliminadas FROM public.cars
UNION ALL SELECT 'quotes',       count(*), count(*) FILTER (WHERE is_deleted) FROM public.quotes
UNION ALL SELECT 'sales',        count(*), 0 FROM public.sales
UNION ALL SELECT 'transactions', count(*), 0 FROM public.transactions;

\echo '== 2. Monedas tras la migración (todo debe ser ARS salvo purchase_cost_currency ya persistido) =='
SELECT 'cars.price_currency' AS columna, price_currency AS moneda, count(*) FROM public.cars GROUP BY 2
UNION ALL SELECT 'cars.purchase_cost_currency', purchase_cost_currency, count(*) FROM public.cars GROUP BY 2
UNION ALL SELECT 'quotes.proposed_price_currency', proposed_price_currency, count(*) FROM public.quotes GROUP BY 2
UNION ALL SELECT 'sales.final_price_currency', final_price_currency, count(*) FROM public.sales GROUP BY 2
UNION ALL SELECT 'transactions.amount_currency', amount_currency, count(*) FROM public.transactions GROUP BY 2
ORDER BY 1, 2;

\echo '== 3. Autos cuyo costo de compra está en otra moneda que el precio (lo mismo que GET dealer/currency-review) =='
SELECT count(*) AS autos_con_monedas_distintas
FROM public.cars
WHERE purchase_cost_currency IS NOT NULL AND purchase_cost_currency <> price_currency;
