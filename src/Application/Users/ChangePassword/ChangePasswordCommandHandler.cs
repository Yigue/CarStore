using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.ChangePassword;

/// <summary>
/// Cambia la contraseña del usuario autenticado, exigiendo la actual.
/// </summary>
/// <remarks>
/// Existía <c>forgot-password</c> / <c>reset-password</c> (flujo por email con
/// token) pero no un cambio autenticado, así que el formulario del perfil no
/// tenía a dónde pegarle y simulaba el guardado.
///
/// Pedir la contraseña actual no es ceremonia: sin eso, cualquiera que agarre
/// una sesión abierta —una laptop sin bloquear, una cookie robada— puede
/// quedarse con la cuenta para siempre cambiando la clave. El token demuestra
/// "esta sesión está viva"; la contraseña actual demuestra "sos vos".
/// </remarks>
internal sealed class ChangePasswordCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPasswordHasher passwordHasher)
    : ICommandHandler<ChangePasswordCommand>
{
    public async Task<Result> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;

        // IgnoreQueryFilters: un SuperAdmin tiene DealerId = Guid.Empty (ADR-1) y no
        // matchea el filtro de tenant, así que sin esto no podría cambiar su propia
        // contraseña.
        User? user = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(userId));
        }

        if (!passwordHasher.Verify(command.CurrentPassword, user.PasswordHash))
        {
            // Mismo error genérico que el login. Acá el usuario ya está autenticado,
            // así que no se filtra nada nuevo, pero mantiene un único mensaje para
            // "la contraseña no coincide" en toda la API.
            return Result.Failure(UserErrors.InvalidPassword);
        }

        user.SetPassword(passwordHasher.Hash(command.NewPassword));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
