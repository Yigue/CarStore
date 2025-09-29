using Application.Cars.Upload;

namespace ApplicationTests.Cars;

public class UploadImageCarCommandValidatorTests
{
    private readonly UploadImageCarCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_ForInvalidValues()
    {
        var command = new UploadImageCarCommand
        {
            CarId = Guid.Empty,
            ImageData = new byte[0],
            FileName = "image.txt",
            IsPrimary = false,
            Order = -1
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UploadImageCarCommand.CarId) && e.ErrorMessage == "El ID del auto es obligatorio");
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UploadImageCarCommand.FileName) && e.ErrorMessage == "Solo se permiten los formatos: .jpg, .jpeg, .png, .webp");
    }

    [Fact]
    public void Validate_ShouldPass_ForValidValues()
    {
        var command = new UploadImageCarCommand
        {
            CarId = Guid.NewGuid(),
            ImageData = new byte[100],
            FileName = "image.jpg",
            IsPrimary = true,
            Order = 0
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
