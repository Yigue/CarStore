-- spr PR 4.2 pre-check (dry run, read-only). H9 / REV-SDD-01.
-- Run against staging/prod BEFORE applying the RemoveTenantUserPermissions migration, and review
-- the report: how many TENANT users (users.role_id <> Guid.Empty) currently depend on a legacy
-- direct grant in "UserPermissions", and which permission strings would be backed up and removed.
--
-- REV-SDD-01: the criterion is users.role_id <> '00000000-0000-0000-0000-000000000000', NOT
-- "UserPermissions".dealer_id — that column is never set by UserPermission's constructor, so it
-- is Guid.Empty on every row regardless of which dealer actually granted it. Platform users
-- (role_id = Guid.Empty) are intentionally excluded: their grants are untouched by the migration.
WITH tenant_grants AS (
    SELECT up.id,
           up.user_id,
           up.permission,
           up.granted_at,
           u.dealer_id,
           u.email,
           u.role_id
    FROM public."UserPermissions" up
    JOIN public.users u ON u.id = up.user_id
    WHERE u.role_id <> '00000000-0000-0000-0000-000000000000'
)
SELECT dealer_id,
       count(DISTINCT user_id) AS tenant_users_with_legacy_grants,
       count(*)                AS total_legacy_grant_rows,
       count(DISTINCT permission) AS distinct_permission_strings,
       min(granted_at)         AS oldest_grant,
       max(granted_at)         AS newest_grant
FROM tenant_grants
GROUP BY dealer_id
ORDER BY tenant_users_with_legacy_grants DESC;

-- Detail (comment out the aggregate above and uncomment below for a full per-row listing before
-- a manual review, e.g. to confirm none of these grants are load-bearing beyond their Role):
--
-- SELECT * FROM tenant_grants ORDER BY dealer_id, user_id;
