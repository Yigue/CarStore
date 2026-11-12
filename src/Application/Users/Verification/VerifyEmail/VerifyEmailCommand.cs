using Application.Abstractions.Messaging;

namespace Application.Users.Verification.VerifyEmail;

public sealed record VerifyEmailCommand(string Token) : ICommand;
