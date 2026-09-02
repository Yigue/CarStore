using FluentValidation;

namespace Application.Platform.Dealers.SuspendDealer;

internal sealed class SuspendDealerCommandValidator : AbstractValidator<SuspendDealerCommand>
{
    public SuspendDealerCommandValidator()
    {
        RuleFor(x => x.DealerId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ETag).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
    }
}
