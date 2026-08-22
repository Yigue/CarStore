using System;
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
        // D7 (qa-p1-integridad PR7, Slice 13): a malformed value must be caught here, not
        // by Convert.FromBase64String in the handler — FormatException stays deliberately
        // unmapped by the global handler (PR1 Slice 1.6), so an uncaught throw there is a 500.
        RuleFor(x => x.Base64Content)
            .Must(BeValidBase64)
            .WithMessage("Base64Content is not valid base64.");
    }

    private static bool BeValidBase64(string base64Content) =>
        Convert.TryFromBase64String(base64Content, new byte[base64Content.Length], out _);
}