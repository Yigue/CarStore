using Application.Abstractions.Messaging;

namespace Application.Cars.Queries.GetCarImages;

public sealed record GetCarImagesQuery(Guid CarId) : IQuery<GetCarImagesResponse>;

/// <summary>Wrapper so the JSON contract is <c>{ "items": [...] }</c> (REQ-VMS-7).</summary>
public sealed record GetCarImagesResponse(IReadOnlyList<CarImageDto> Items);
