using SharedKernel;

namespace Domain.Users;

/// <summary>sfc B6.3 / RN-043: errors for the dealer-selection/switch flow and membership grants.</summary>
public static class DealerAccessErrors
{
    /// <summary>The caller is not in the user's effective membership set (home dealer + grants).</summary>
    public static readonly Error NotAMember = Error.Forbidden(
        "DealerAccess.NotAMember",
        "El usuario no tiene acceso a ese dealer.");

    /// <summary>
    /// `auth/select-dealer` was called with something other than a pending-selection token (e.g. a
    /// normal full session token replayed), or `auth/switch-dealer` was called with a pending token
    /// instead of a full session.
    /// </summary>
    public static readonly Error InvalidStage = Error.Forbidden(
        "DealerAccess.InvalidStage",
        "Este token no es válido para esta operación.");

    public static Error AlreadyGranted(Guid userId, Guid dealerId) => Error.Conflict(
        "DealerAccess.AlreadyGranted",
        $"El usuario '{userId}' ya tiene acceso al dealer '{dealerId}'.");
}
