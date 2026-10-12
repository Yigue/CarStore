using SharedKernel;

namespace Application.Users.OwnerTransfer;

public static class OwnerTransferErrors
{
    public static readonly Error NotOwner = Error.Forbidden(
        "OwnerTransfer.NotOwner", "Solo el titular de la cuenta puede transferir la titularidad.");

    public static readonly Error TargetNotFound = Error.NotFound(
        "OwnerTransfer.TargetNotFound", "No se encontró el usuario de destino.");

    public static readonly Error SameUser = Error.Validation(
        "OwnerTransfer.SameUser", "Ya sos el titular de la cuenta.");

    public static readonly Error TargetNotEligible = Error.Conflict(
        "OwnerTransfer.TargetNotEligible",
        "El destinatario debe ser un administrador activo de la agencia con el email verificado.");

    public static readonly Error EmailNotSent = Error.Failure(
        "OwnerTransfer.EmailNotSent", "No pudimos enviar el email de confirmación. Intentá de nuevo en unos minutos.");

    public static readonly Error InvalidToken = Error.Problem(
        "OwnerTransfer.InvalidToken", "El enlace de transferencia es inválido o ha expirado.");
}
