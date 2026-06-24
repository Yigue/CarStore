using FluentValidation;

namespace Application.Cars.Commands.UploadCarImage;

public sealed class UploadCarImageCommandValidator : AbstractValidator<UploadCarImageCommand>
{
    public UploadCarImageCommandValidator()
    {
        RuleFor(x => x.CarId).NotEmpty();

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .Must(ct => CarImageUploadConstraints.AllowedContentTypes.Contains(ct))
            .WithMessage("Content type must be one of: image/jpeg, image/png, image/webp.");

        RuleFor(x => x.FileName)
            .NotEmpty()
            .MaximumLength(CarImageUploadConstraints.MaxFileNameLength);

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0)
            .LessThanOrEqualTo(CarImageUploadConstraints.MaxUploadSizeBytes)
            .WithMessage("Image exceeds the maximum allowed size (10 MB).");
    }
}
