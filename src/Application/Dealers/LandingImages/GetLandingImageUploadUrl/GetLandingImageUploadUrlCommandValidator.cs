using FluentValidation;

namespace Application.Dealers.LandingImages.GetLandingImageUploadUrl;

public sealed class GetLandingImageUploadUrlCommandValidator : AbstractValidator<GetLandingImageUploadUrlCommand>
{
    public GetLandingImageUploadUrlCommandValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .MaximumLength(LandingImageUploadConstraints.MaxFileNameLength);

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .Must(ct => LandingImageUploadConstraints.AllowedContentTypes.Contains(ct))
            .WithMessage("Content type must be one of: image/jpeg, image/png, image/webp.");
    }
}
