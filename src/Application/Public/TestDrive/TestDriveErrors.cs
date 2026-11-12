using SharedKernel;

namespace Application.Public.TestDrive;

public static class TestDriveErrors
{
    public static Error CarNotFound(Guid carId) => Error.NotFound(
        "TestDrive.CarNotFound",
        $"Vehículo con ID {carId} no encontrado.");

    /// <summary>
    /// RN-044: at most two simultaneous <c>Requested</c> test-drive appointments per contact
    /// (email or phone) per dealer. The Web.Api endpoint maps this specific code to HTTP 429
    /// (see <c>Web.Api.Endpoints.Public.CreateTestDrive</c>) — it is a business-rule throttle, not
    /// the per-IP rate-limiting middleware policy, so it does not fit the existing
    /// <see cref="ErrorType"/>/<c>CustomResults</c> status mapping and is special-cased there
    /// instead of widening that shared switch for one caller.
    /// </summary>
    public static readonly Error TooManyRequested = Error.Problem(
        "TestDrive.TooManyRequested",
        "Ya hay dos solicitudes de test drive pendientes de confirmación para este contacto.");

    public static readonly Error OutsideBusinessHours = Error.Validation(
        "TestDrive.OutsideBusinessHours",
        "El horario elegido está fuera del horario de atención del concesionario.");

    public static readonly Error Conflict = Error.Problem(
        "TestDrive.Conflict",
        "El vehículo ya tiene un turno en ese horario.");
}
