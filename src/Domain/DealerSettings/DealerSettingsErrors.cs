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
}
