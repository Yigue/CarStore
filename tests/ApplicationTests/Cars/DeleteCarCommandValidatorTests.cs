using Application.Cars.Delete;

namespace ApplicationTests.Cars;

public class DeleteCarCommandValidatorTests
{
    private readonly DeleteCarCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenCarIdEmpty()
    {
        var command = new DeleteCarCommand(Guid.Empty);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(DeleteCarCommand.CarId) && e.ErrorMessage == "'Car Id' must not be empty.");
    }

    [Fact]
    public void Validate_ShouldPass_WhenCarIdProvided()
    {
        var command = new DeleteCarCommand(Guid.NewGuid());

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
