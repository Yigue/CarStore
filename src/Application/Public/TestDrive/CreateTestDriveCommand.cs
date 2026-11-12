using Application.Abstractions.Messaging;

namespace Application.Public.TestDrive;

/// <summary>
/// sfc B5.2.2/B5.2.4 / RN-044: public, anonymous booking of a test drive. <c>DealerId</c> is
/// resolved server-side from the vehicle, never trusted from the client (same convention as
/// <c>CreateInquiryCommand</c>/<c>CreateAppointmentCommand</c>).
/// <c>CaptchaToken</c> is NOT part of this command: it is verified by
/// <c>Web.Api.Infrastructure.CaptchaEndpointFilter</c> before the handler ever runs (same
/// convention as <c>Provision</c> — see that endpoint). <paramref name="Website"/> is the
/// honeypot: a hidden form field no human fills in; a non-empty value is treated as a bot and
/// the handler fakes a normal success response while creating nothing (see the handler).
/// </summary>
public sealed record CreateTestDriveCommand(
    Guid CarId,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    DateTime Start,
    string? Comments,
    string? Website) : ICommand<Guid>;
