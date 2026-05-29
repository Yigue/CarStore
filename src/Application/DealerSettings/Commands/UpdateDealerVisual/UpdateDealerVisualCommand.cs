using Application.Abstractions.Messaging;

namespace Application.DealerSettings.Commands.UpdateDealerVisual;

public sealed record UpdateDealerVisualCommand(
    string? LogoUrl,
    string? PrimaryColor,
    string? SecondaryColor,
    string? FooterText
) : ICommand<Guid>;