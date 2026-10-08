using FluentValidation;

namespace Application.DealerSettings.Commands.UpdateDealerVisual;

public sealed class UpdateDealerVisualCommandValidator : AbstractValidator<UpdateDealerVisualCommand>
{
    public UpdateDealerVisualCommandValidator()
    {
        RuleFor(x => x.LogoUrl)
            .Must(BeAValidHttpOrHttpsUrl)
            .When(x => !string.IsNullOrEmpty(x.LogoUrl))
            .WithMessage("LogoUrl must be a valid absolute HTTP or HTTPS URL.");
    }

    private static bool BeAValidHttpOrHttpsUrl(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
