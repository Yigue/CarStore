-- scripts/dedupe-marcas.sql
-- Pre-migration deduplication for MarcaNombreNormalizado.
--
-- Run BEFORE `dotnet ef database update` if duplicate brands exist with different casing/whitespace.
-- For each group of duplicates sharing the same upper(trim(nombre)):
-- 1. Identifies the canonical marca (first created / min id).
-- 2. Repoints all dependent modelos to the canonical marca.
-- 3. Repoints all dependent cars to the canonical marca.
-- 4. Deletes the duplicate non-canonical marca rows.

BEGIN;

CREATE TEMP TABLE IF NOT EXISTS marca_mapping AS
WITH ranked AS (
    SELECT 
        id,
        nombre,
        upper(trim(nombre)) AS norm,
        FIRST_VALUE(id) OVER (PARTITION BY upper(trim(nombre)) ORDER BY id) AS canonical_id
    FROM public.marca
)
SELECT id AS duplicate_id, canonical_id
FROM ranked
WHERE id <> canonical_id;

-- Repoint modelos
UPDATE public.modelo m
SET marca_id = map.canonical_id
FROM marca_mapping map
WHERE m.marca_id = map.duplicate_id;

-- Repoint cars
UPDATE public.cars c
SET marca_id = map.canonical_id
FROM marca_mapping map
WHERE c.marca_id = map.duplicate_id;

-- Delete duplicate marcas
DELETE FROM public.marca m
USING marca_mapping map
WHERE m.id = map.duplicate_id;

DROP TABLE marca_mapping;

COMMIT;

-- Diagnostic check (expect 0 rows):
-- SELECT upper(trim(nombre)), count(*) 
-- FROM public.marca 
-- GROUP BY 1 
-- HAVING count(*) > 1;
