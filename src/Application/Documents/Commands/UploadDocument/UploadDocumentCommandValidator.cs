using FluentValidation;

namespace Application.Documents.Commands.UploadDocument;

public sealed class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContentType)
            .Must(x => x is "application/pdf" or "image/jpeg" or "image/png");
        RuleFor(x => x.Base64Content)
            .Must(x => x.Length < 13_300_000);
    }
}