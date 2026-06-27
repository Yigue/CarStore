namespace Web.Api.Endpoints.Clients;

internal static class Permissions
{
    // Q1A: consolidated to read | write | delete (spec-aligned)
    internal const string ClientsRead = "clients:read";
    internal const string ClientsWrite = "clients:write";
    internal const string ClientsDelete = "clients:delete";

    // Aliases kept for one-PR transition (removed in PR3 migration 20260625000002)
    [Obsolete("Use ClientsWrite — removed in PR3")]
    internal const string ClientsCreate = "clients:write";
    [Obsolete("Use ClientsWrite — removed in PR3")]
    internal const string ClientsUpdate = "clients:write";
}

