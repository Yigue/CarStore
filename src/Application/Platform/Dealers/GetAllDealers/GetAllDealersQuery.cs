using Application.Abstractions.Messaging;
using Application.Platform.Common;

namespace Application.Platform.Dealers.GetAllDealers;

public sealed record GetAllDealersQuery : IQuery<IReadOnlyList<PlatformDealerResponse>>;
