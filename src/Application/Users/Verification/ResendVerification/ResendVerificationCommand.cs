using Application.Abstractions.Messaging;

namespace Application.Users.Verification.ResendVerification;

public sealed record ResendVerificationCommand(string Email) : ICommand;
