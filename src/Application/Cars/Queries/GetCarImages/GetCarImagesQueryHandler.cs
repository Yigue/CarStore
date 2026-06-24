using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.Queries.GetCarImages;

internal sealed class GetCarImagesQueryHandler(
    IApplicationDbContext context,
    IStorageService storage,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetCarImagesQuery, GetCarImagesResponse>
{
    private static readonly TimeSpan ReadTtl = TimeSpan.FromMinutes(15);

    public async Task<Result<GetCarImagesResponse>> Handle(
        GetCarImagesQuery query,
        CancellationToken cancellationToken)
    {
        bool carExists = await context.Cars
            .AsNoTracking()
            .AnyAsync(c => c.Id == query.CarId, cancellationToken);

        if (!carExists)
        {
            return Result.Failure<GetCarImagesResponse>(CarErrors.NotFound(query.CarId));
        }

        List<CarImage> images = await context.CarImages
            .AsNoTracking()
            .Where(i => i.CarId == query.CarId)
            .OrderBy(i => i.DisplayOrder)
            .ToListAsync(cancellationToken);

        var items = new List<CarImageDto>(images.Count);

        foreach (CarImage image in images)
        {
            string url;
            DateTime expiresAt;

            if (image.ObjectKey is not null)
            {
                Uri presigned = await storage.GetPresignedUrlAsync(image.ObjectKey, ReadTtl, cancellationToken);
                url = presigned.ToString();
                expiresAt = dateTimeProvider.UtcNow.Add(ReadTtl);
            }
            else
            {
                // Legacy image: direct URL, no expiry.
                url = image.ImageUrl ?? string.Empty;
                expiresAt = DateTime.MaxValue;
            }

            items.Add(new CarImageDto(image.Id, url, image.IsCover, image.DisplayOrder, expiresAt));
        }

        return Result.Success(new GetCarImagesResponse(items));
    }
}
