using FluentValidation;

namespace Application.DealerSettings.Update;

/// <summary>
/// Field rules for <c>PUT /dealer-settings</c>. Lengths mirror the database columns, and the
/// social links must be absolute http(s) URLs: they are rendered as hrefs on the public landing,
/// so <c>javascript:</c> or <c>data:</c> values would be stored XSS.
/// </summary>
public sealed class UpdateDealerSettingsCommandValidator : AbstractValidator<UpdateDealerSettingsCommand>
{
    public UpdateDealerSettingsCommandValidator()
    {
        RuleFor(x => x.DealerName)
            .NotEmpty().WithMessage("El nombre de la concesionaria es requerido.")
            .MaximumLength(200).WithMessage("El nombre no puede superar los 200 caracteres.");

        RuleFor(x => x.ContactEmail)
            .NotEmpty().WithMessage("El email de contacto es requerido.")
            .EmailAddress().WithMessage("El email de contacto no es válido.")
            .MaximumLength(200).WithMessage("El email no puede superar los 200 caracteres.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(50).WithMessage("El teléfono no puede superar los 50 caracteres.");

        RuleFor(x => x.Address)
            .MaximumLength(300).WithMessage("La dirección no puede superar los 300 caracteres.");

        RuleFor(x => x.FacebookUrl).MustBeHttpUrl();
        RuleFor(x => x.InstagramUrl).MustBeHttpUrl();
        RuleFor(x => x.TwitterUrl).MustBeHttpUrl();

        RuleFor(x => x.InterestRateTna)
            .InclusiveBetween(0m, 1000m)
            .When(x => x.InterestRateTna.HasValue)
            .WithMessage("La TNA debe estar entre 0 y 1000.");
    }
}

internal static class HttpUrlRule
{
    public static IRuleBuilderOptions<T, string?> MustBeHttpUrl<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .MaximumLength(500).WithMessage("La URL no puede superar los 500 caracteres.")
            .Must(url => string.IsNullOrEmpty(url)
                         || (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                             && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
            .WithMessage("La URL debe ser absoluta y usar http o https.");
}
