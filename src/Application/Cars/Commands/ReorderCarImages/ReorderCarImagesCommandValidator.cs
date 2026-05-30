using FluentValidation;

namespace Application.Cars.Commands.ReorderCarImages;

public sealed class ReorderCarImagesCommandValidator : AbstractValidator<ReorderCarImagesCommand>
{
    public ReorderCarImagesCommandValidator()
    {
        RuleFor(x => x.CarId).NotEmpty();

        RuleFor(x => x.OrderedImageIds)
            .NotNull()
            .NotEmpty()
            .WithMessage("orderedImageIds must contain at least one image id.");

        RuleFor(x => x.OrderedImageIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("orderedImageIds must not contain duplicates.")
            .When(x => x.OrderedImageIds is not null);
    }
}
