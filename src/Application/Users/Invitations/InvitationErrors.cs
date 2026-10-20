using SharedKernel;

namespace Application.Users.Invitations;

/// <summary>sfc A4.2 / A14 / RN-031: what can go wrong inviting someone into a dealership by email.</summary>
public static class InvitationErrors
{
    public static readonly Error InvalidToken = Error.Problem(
        "Invitation.InvalidToken", "El enlace de invitación es inválido, ya fue usado o ha expirado.");

    public static readonly Error EmailNotSent = Error.Failure(
        "Invitation.EmailNotSent", "No pudimos enviar el email de invitación. Intentá de nuevo en unos minutos.");
}
