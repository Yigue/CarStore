using MediatR;
using SharedKernel;

namespace Application.Cars.RegenerateImageUrls;

public sealed record RegenerateImageUrlsCommand : IRequest<Result<int>>;
