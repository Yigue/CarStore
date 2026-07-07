using SharedKernel;

namespace Domain.DealerSettings;

public static class DealerSettingsErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "DealerSettings.NotFound",
        "No se encontraron settings para esta concesionaria. Probablemente nunca fueron inicializados.");

    public static readonly Error Unauthorized = Error.Failure(
        "DealerSettings.Unauthorized",
        "No tenés permisos para modificar la configuración de esta concesionaria.");

    /// <summary>
    /// Returned when a PUT /dealer-settings/hostname attempt collides with an existing
    /// HostName or Slug unique index (HTTP 409). PR1 task 1.5.1.
    /// </summary>
    public static readonly Error HostNameConflict = Error.Conflict(
        "DealerSettings.HostNameConflict",
        "The requested HostName or Slug is already in use by another dealer.");
}
