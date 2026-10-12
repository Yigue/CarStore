using Application.Abstractions.Messaging;

namespace Application.Users.OwnerTransfer.InitiateOwnerTransfer;

public sealed record InitiateOwnerTransferCommand(Guid TargetUserId) : ICommand;
