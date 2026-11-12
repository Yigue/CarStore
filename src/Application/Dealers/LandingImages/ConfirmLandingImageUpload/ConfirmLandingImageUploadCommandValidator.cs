using FluentValidation;

namespace Application.Dealers.LandingImages.ConfirmLandingImageUpload;

public sealed class ConfirmLandingImageUploadCommandValidator : AbstractValidator<ConfirmLandingImageUploadCommand>
{
    public ConfirmLandingImageUploadCommandValidator()
    {
        RuleFor(x => x.ImageId).NotEmpty();

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .Must(ct => LandingImageUploadConstraints.AllowedContentTypes.Contains(ct))
            .WithMessage("Content type must be one of: image/jpeg, image/png, image/webp.");

        RuleFor(x => x.FileName)
            .NotEmpty()
            .MaximumLength(LandingImageUploadConstraints.MaxFileNameLength);

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0)
            .LessThanOrEqualTo(LandingImageUploadConstraints.MaxUploadSizeBytes)
            .WithMessage("Image exceeds the maximum allowed size (5 MB).");
    }
}
