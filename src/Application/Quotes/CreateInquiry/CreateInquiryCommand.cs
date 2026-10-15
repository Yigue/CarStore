using Application.Abstractions.Messaging;

namespace Application.Quotes.CreateInquiry;

public sealed record CreateInquiryCommand(
    Guid? CarId,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Comments,
    // sfc C0.1 / RN-045: the visitor's first touch, captured by the public site. Optional.
    InquiryAttribution? Attribution = null) : ICommand<Guid>;

/// <summary>sfc C0.1: first-touch attribution as sent by the public site (all optional except the timestamp).</summary>
public sealed record InquiryAttribution(
    string? UtmSource,
    string? UtmMedium,
    string? UtmCampaign,
    string? UtmTerm,
    string? UtmContent,
    string? Referrer,
    string? LandingPath,
    string? Gclid,
    string? Fbclid,
    DateTime FirstTouchAtUtc);
