using SharedKernel;

namespace Application.Tenancy.Export;

public static class TenantExportErrors
{
    public static readonly Error NotFound = new(
        "TenantExport.NotFound", "No se encontró la exportación solicitada.", ErrorType.NotFound);

    public static readonly Error NotAvailable = new(
        "TenantExport.NotAvailable", "La exportación no está disponible para el estado actual de la cuenta.", ErrorType.Forbidden);

    public static readonly Error AlreadyInProgress = new(
        "TenantExport.AlreadyInProgress", "Ya hay una exportación en curso. Esperá a que termine para pedir otra.", ErrorType.Conflict);

    public static readonly Error RateLimited = new(
        "TenantExport.RateLimited", "Solo se permite una exportación cada 24 horas.", ErrorType.Conflict);
}
