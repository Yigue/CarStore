using FluentValidation;

namespace Application.Sales.Create;

internal sealed class CreateSaleCommandValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleCommandValidator()
    {
        RuleFor(x => x.CarId).NotEmpty();
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.FinalPrice).NotEmpty().GreaterThan(0);
        RuleFor(x => x.PaymentMethod).IsInEnum();
        
    }
}
