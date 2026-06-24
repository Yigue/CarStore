using SharedKernel;

namespace Domain.Users;

public static class UserErrors
{
    public static Error NotFound(Guid userId) => Error.NotFound(
        "Users.NotFound",
        $"The user with the Id = '{userId}' was not found");

    public static Error Unauthorized() => Error.Failure(
        "Users.Unauthorized",
        "You are not authorized to perform this action.");

    public static readonly Error NotFoundByEmail = Error.NotFound(
        "Users.NotFoundByEmail",
        "The user with the specified email was not found");

    public static readonly Error EmailNotUnique = Error.Conflict(
        "Users.EmailNotUnique",
        "The provided email is not unique");

    public static readonly Error InvalidPassword = Error.Problem(
        "Users.InvalidPassword",
        "The password provided is incorrect");

    public static readonly Error InvalidResetToken = Error.Problem(
        "Users.InvalidResetToken",
        "El token de recuperación es inválido o ha expirado");

    public static readonly Error SelfDeleteNotAllowed = Error.Failure(
        "Users.SelfDeleteNotAllowed",
        "No puedes eliminar tu propia cuenta");

    public static readonly Error CannotRevokeOwnPermission = Error.Failure(
        "Users.CannotRevokeOwnPermission",
        "No puedes revocar tus propios permisos de administrador");
}
