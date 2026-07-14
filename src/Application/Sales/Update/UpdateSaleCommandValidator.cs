using FluentValidation;

namespace Application.Sales.Update;

internal sealed class UpdateSaleCommandValidator : AbstractValidator<UpdateSaleCommand>
{
    public UpdateSaleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FinalPrice).GreaterThan(0);

        // ContractNumber/Comments are optional on update (the frontend may omit them) —
        // only enforce a max length when a value is actually provided.
        RuleFor(x => x.ContractNumber).MaximumLength(50).When(x => x.ContractNumber != null);
        RuleFor(x => x.Comments).MaximumLength(500).When(x => x.Comments != null);

        RuleFor(x => x.SalespersonId)
            .NotEqual(Guid.Empty)
            .When(x => x.SalespersonId.HasValue)
            .WithMessage("SalespersonId cannot be an empty guid.");
    }
}
