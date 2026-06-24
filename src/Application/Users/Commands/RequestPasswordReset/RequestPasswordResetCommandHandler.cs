using System.Security.Cryptography;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Users.GetByEmail;
using Domain.Users;
using MediatR;
using Microsoft.Extensions.Configuration;
using SharedKernel;

namespace Application.Users.Commands.RequestPasswordReset;

public sealed record RequestPasswordResetCommand(string Email) : ICommand;

internal sealed class RequestPasswordResetCommandHandler(
    ISender sender,
    IApplicationDbContext context,
    IEmailService emailService,
    IConfiguration configuration
) : ICommandHandler<RequestPasswordResetCommand>
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    public async Task<Result> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        // Check if user exists
        var userResult = await sender.Send(new GetUserByEmailQuery(request.Email), cancellationToken);

        if (userResult.IsFailure)
        {
            // Do not leak whether the user exists or not
            return Result.Success();
        }

        UserResponse user = userResult.Value;

        // Single-use, time-limited token. Stored hashed-free (it is already a high-entropy
        // 256-bit random value) and looked up directly when the user submits the reset.
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        DateTime expiresAt = DateTime.UtcNow.Add(TokenLifetime);

        context.PasswordResetTokens.Add(new PasswordResetToken(user.Id, token, expiresAt));
        await context.SaveChangesAsync(cancellationToken);

        string baseUrl = (configuration["Frontend:BaseUrl"] ?? "https://carstore-app.com").TrimEnd('/');
        string resetLink = $"{baseUrl}/reset-password?token={token}";

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
