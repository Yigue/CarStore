using System.Security.Cryptography;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Users.ResetPassword;
using Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SharedKernel;

namespace Application.Users.Commands.RequestPasswordReset;

public sealed record RequestPasswordResetCommand(string Email) : ICommand;

internal sealed class RequestPasswordResetCommandHandler(
    IApplicationDbContext context,
    IEmailService emailService,
    IConfiguration configuration
) : ICommandHandler<RequestPasswordResetCommand>
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    public async Task<Result> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        // The caller is anonymous by definition: this must NOT go through GetUserByEmailQuery, which is
        // "my own profile by email" and compares the match against the authenticated user — with no session
        // it threw "User id is unavailable" and forgot-password answered 500 for everybody, so nobody could
        // ever recover a password.
        //
        // Tenant filters are bypassed on purpose (no tenant is resolved before login) and the lookup is by
        // the unique email only. Unknown or deactivated accounts get the same neutral success as known ones,
        // so the response never reveals whether an email exists.
        var user = await context.Users
            .IgnoreQueryFilters()
            .Where(u => u.Email == request.Email && u.IsActive)
            .Select(u => new { u.Id, u.FirstName })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return Result.Success();
        }

        // Single-use, time-limited token. Only its SHA-256 digest is stored; the plain value
        // travels in the email and is hashed again when the user submits the reset.
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        DateTime expiresAt = DateTime.UtcNow.Add(TokenLifetime);

        context.PasswordResetTokens.Add(new PasswordResetToken(user.Id, PasswordResetTokenHasher.Hash(token), expiresAt));
        await context.SaveChangesAsync(cancellationToken);

        string baseUrl = (configuration["Frontend:BaseUrl"] ?? "https://carstore-app.com").TrimEnd('/');
        string resetLink = $"{baseUrl}/auth/reset-password?token={token}";

        string subject = "Recuperación de contraseña - CarStore";
        string body = $@"
Hola {user.FirstName},

Hemos recibido una solicitud para restablecer tu contraseña.
Haz clic en el siguiente enlace para crear una nueva contraseña (válido por 1 hora):
{resetLink}

Si no solicitaste este cambio, ignora este correo.

Saludos,
El equipo de CarStore";

        await emailService.SendEmailAsync(request.Email, subject, body, cancellationToken);

        return Result.Success();
    }
}
