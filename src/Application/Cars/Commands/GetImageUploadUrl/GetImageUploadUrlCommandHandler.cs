using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.Commands.GetImageUploadUrl;

internal sealed class GetImageUploadUrlCommandHandler(
    IApplicationDbContext context,
    IStorageService storage,
    ICurrentTenantService tenant)
    : ICommandHandler<GetImageUploadUrlCommand, ImageUploadUrlResponse>
{
    private static readonly TimeSpan UploadTtl = TimeSpan.FromMinutes(10);

    public async Task<Result<ImageUploadUrlResponse>> Handle(
        GetImageUploadUrlCommand command,
        CancellationToken cancellationToken)
    {
        bool carExists = await context.Cars
            .AnyAsync(c => c.Id == command.CarId, cancellationToken);

        if (!carExists)
        {
            return Result.Failure<ImageUploadUrlResponse>(CarErrors.NotFound(command.CarId));
        }

        Guid imageId = Guid.NewGuid();
        // Use the same logic for object key as the standard upload
        string ext = Path.GetExtension(command.FileName).TrimStart('.');
        if (string.IsNullOrEmpty(ext)) ext = "jpg";
        
        string objectKey = $"cars/{tenant.DealerId}/{command.CarId}/{imageId}.{ext}";

        (string url, IReadOnlyDictionary<string, string> fields) = 
            await storage.GeneratePresignedPostAsync(objectKey, command.ContentType, UploadTtl, cancellationToken);

        return new ImageUploadUrlResponse(imageId, url, fields);
    }
}
