namespace Web.Api.Endpoints.Leads;

internal static class Permissions
{
    // Q1A: consolidated to read | write | archive (spec-aligned)
    internal const string LeadsRead = "leads:read";
    internal const string LeadsWrite = "leads:write";
    internal const string LeadsArchive = "leads:archive";

    // Aliases kept for one-PR transition (removed in PR3 migration 20260625000002)
    [Obsolete("Use LeadsWrite — removed in PR3")]
    internal const string LeadsCreate = "leads:write";
    [Obsolete("Use LeadsWrite — removed in PR3")]
    internal const string LeadsUpdate = "leads:write";
}
