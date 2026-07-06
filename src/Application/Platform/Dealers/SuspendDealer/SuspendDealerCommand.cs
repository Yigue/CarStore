using Application.Abstractions.Messaging;
using Application.Platform.Common;

namespace Application.Platform.Dealers.SuspendDealer;

public sealed record SuspendDealerCommand(
    Guid DealerId,
    string Reason,
    string ETag,
    Guid ActorId = default) : ICommand<PlatformDealerResponse>;
