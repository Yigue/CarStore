using Application.Abstractions.Messaging;

namespace Application.Quotes.CreateInquiry;

public sealed record CreateInquiryCommand(
    Guid? CarId,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Comments) : ICommand<Guid>;
