using FluentValidation;
using System.IO;

namespace Application.Cars.Upload;

public class UploadImageCarCommandValidator : AbstractValidator<UploadImageCarCommand>
{
    private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

    public UploadImageCarCommandValidator()
    {
        RuleFor(x => x.CarId)
            .NotEmpty().WithMessage("El ID del auto es obligatorio");
        
        RuleFor(x => x.ImageData)
            .NotEmpty().WithMessage("La imagen es obligatoria")
            .Must(x => x.Length <= MaxFileSizeBytes)
                .WithMessage($"El tamaño máximo permitido es {MaxFileSizeBytes / (1024 * 1024)}MB");
        
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("El nombre del archivo es obligatorio")
            .Must(filename => _allowedExtensions.Any(ext => 
                string.Equals(Path.GetExtension(filename), ext, StringComparison.OrdinalIgnoreCase)))
                .WithMessage($"Solo se permiten los formatos: {string.Join(", ", _allowedExtensions)}");
        
        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0).WithMessage("El orden debe ser un número positivo");
    }
} 