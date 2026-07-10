using FluentValidation;

namespace Application.Platform.Dealers.ActivateDealer;

internal sealed class ActivateDealerCommandValidator : AbstractValidator<ActivateDealerCommand>
{
    public ActivateDealerCommandValidator()
    {
        RuleFor(x => x.DealerId).NotEmpty();
        RuleFor(x => x.ETag).NotEmpty();
    }
}
