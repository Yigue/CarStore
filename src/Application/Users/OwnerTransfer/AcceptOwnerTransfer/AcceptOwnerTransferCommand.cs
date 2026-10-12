using Application.Abstractions.Messaging;

namespace Application.Users.OwnerTransfer.AcceptOwnerTransfer;

public sealed record AcceptOwnerTransferCommand(string Token) : ICommand;
