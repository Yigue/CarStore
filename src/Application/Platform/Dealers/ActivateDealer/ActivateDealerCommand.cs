using Application.Abstractions.Messaging;
using Application.Platform.Common;

namespace Application.Platform.Dealers.ActivateDealer;

public sealed record ActivateDealerCommand(
    Guid DealerId,
    string ETag) : ICommand<PlatformDealerResponse>;
