namespace Domain.Tenancy;

/// <summary>RN-014 (A7) lifecycle of a tenant data export.</summary>
public enum TenantExportStatus
{
    Requested = 0,
    Running = 1,
    Ready = 2,
    Expired = 3,
    Failed = 4
}
