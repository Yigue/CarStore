-- sfc B5.1.4 pre-check (dry run, read-only). Run against staging before applying the AddSubscribers migration and
-- review the report: how many legacy newsletter placeholder clients each dealer has, how many become subscribers,
-- and how many are deleted versus soft-deleted because something references them.
WITH legacy AS (
    SELECT c.*,
        EXISTS (SELECT 1 FROM public.quotes x WHERE x.client_id = c.id)
        OR EXISTS (SELECT 1 FROM public.sales x WHERE x.client_id = c.id)
        OR EXISTS (SELECT 1 FROM public.deposits x WHERE x.client_id = c.id)
        OR EXISTS (SELECT 1 FROM public.appointments x WHERE x.client_id = c.id)
        OR EXISTS (SELECT 1 FROM public.documents x WHERE x.client_id = c.id)
        OR EXISTS (SELECT 1 FROM public.transactions x WHERE x.client_id = c.id)
        OR EXISTS (SELECT 1 FROM public.warranty_records x WHERE x.client_id = c.id)
        OR EXISTS (SELECT 1 FROM public.service_reminders x WHERE x.client_id = c.id)
        OR EXISTS (SELECT 1 FROM public.leads x WHERE x.converted_client_id = c.id) AS referenced
    FROM public.clients c
    WHERE c.dni LIKE 'NL-%' AND c.first_name = 'Newsletter'
)
SELECT dealer_id,
       count(*)                                                                   AS legacy_clients,
       count(DISTINCT lower(trim(email))) FILTER (WHERE NOT is_deleted
           AND coalesce(trim(email), '') <> '')                                   AS subscribers_to_create,
       count(*) FILTER (WHERE NOT referenced)                                     AS clients_to_delete,
       count(*) FILTER (WHERE referenced AND NOT is_deleted)                      AS clients_to_soft_delete
FROM legacy
GROUP BY dealer_id
ORDER BY dealer_id;
