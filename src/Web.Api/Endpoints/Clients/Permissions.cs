namespace Web.Api.Endpoints.Clients;

/// <summary>
/// Canonical CRM client permission strings. Aliased from clients:create/clients:update (PR1)
/// to clients:write; old aliases removed by migration DropLegacyCRMPermissions (PR3).
/// </summary>
internal static class Permissions
{
    internal const string ClientsRead = "clients:read";
    internal const string ClientsWrite = "clients:write";
    internal const string ClientsDelete = "clients:delete";

    /// <summary>
    /// Same string as <c>Cars.Permissions.AdminBackfill</c>: one admin-only permission gates
    /// every backfill endpoint regardless of the aggregate it repairs.
    /// </summary>
    internal const string AdminBackfill = "admin:backfill";
}

