using FluentValidation;

namespace Application.Sales.Update;

internal sealed class UpdateSaleCommandValidator : AbstractValidator<UpdateSaleCommand>
{
    public UpdateSaleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FinalPrice).GreaterThan(0);
        RuleFor(x => x.ContractNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Comments).NotEmpty().MaximumLength(500);
    }
}
