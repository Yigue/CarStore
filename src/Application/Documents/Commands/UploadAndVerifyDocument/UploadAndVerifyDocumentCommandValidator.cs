using FluentValidation;

namespace Application.Documents.Commands.UploadAndVerifyDocument;

internal sealed class UploadAndVerifyDocumentCommandValidator : AbstractValidator<UploadAndVerifyDocumentCommand>
{
    private static readonly string[] AllowedContentTypes =
    [
        "application/pdf",
        "image/jpeg",
        "image/jpg",
        "image/png",
    ];

    public UploadAndVerifyDocumentCommandValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .Must(ct => AllowedContentTypes.Contains(ct, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Tipo de archivo no soportado. Permitidos: pdf, jpeg, jpg, png.");

        RuleFor(x => x.FileStream)
            .NotNull();
    }
}
