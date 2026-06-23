using Application.Abstractions.Messaging;
using Application.Users.GetByEmail;
using MediatR;
using SharedKernel;

namespace Application.Users.Commands.RequestPasswordReset;

public sealed record RequestPasswordResetCommand(string Email) : ICommand;

internal sealed class RequestPasswordResetCommandHandler(
    ISender sender,
    IEmailService emailService
) : ICommandHandler<RequestPasswordResetCommand>
{
    public async Task<Result> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        // Check if user exists
        var userResult = await sender.Send(new GetUserByEmailQuery(request.Email), cancellationToken);

        if (userResult.IsFailure)
        {
            // Do not leak whether the user exists or not
            return Result.Success();
        }

        // Generate a reset link (placeholder for now, you could use a signed JWT or store a token in DB)
        // In a real scenario, the link should point to the frontend `/auth/recuperar?token=...`
        string resetLink = $"https://carstore-app.com/auth/recuperar?token=mock_token_for_{userResult.Value.Id}";

        string subject = "Recuperación de contraseña - CarStore";
        string body = $@"
Hola {userResult.Value.FirstName},

Hemos recibido una solicitud para restablecer tu contraseña.
Haz clic en el siguiente enlace para crear una nueva contraseña:
{resetLink}

Si no solicitaste este cambio, ignora este correo.

Saludos,
El equipo de CarStore";

        await emailService.SendEmailAsync(request.Email, subject, body, cancellationToken);

        return Result.Success();
    }
}
