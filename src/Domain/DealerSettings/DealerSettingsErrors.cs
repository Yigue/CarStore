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
    /// Raised when a unique-index violation on <c>HostName</c> surfaces from the database.
    /// Mapped to HTTP 409 by the API layer.
    /// </summary>
    public static readonly Error HostNameNotUnique = Error.Conflict(
        "DealerSettings.HostName",
        "Subdomain already taken");

    /// <summary>
    /// Raised when a caller attempts to provision a reserved subdomain slug.
    /// The blocklist is enforced in <c>ProvisionDealerCommandValidator</c> as well;
    /// this error exists for paths that bypass the validator (e.g. raw EF writes).
    /// </summary>
    public static Error ReservedSubdomain(string name) => Error.Validation(
        "DealerSettings.HostName",
        $"'{name}' is reserved");
}
