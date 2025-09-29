using SharedKernel;

public class ErrorTests
{
    [Theory]
    [InlineData("code1", "desc1", ErrorType.Failure)]
    [InlineData("code2", "desc2", ErrorType.Problem)]
    [InlineData("code3", "desc3", ErrorType.NotFound)]
    [InlineData("code4", "desc4", ErrorType.Conflict)]
    public void FactoryMethods_CreateErrorsWithCorrectType(string code, string desc, ErrorType type)
    {
        // Act
        Error error = type switch
        {
            ErrorType.Problem => Error.Problem(code, desc),
            ErrorType.NotFound => Error.NotFound(code, desc),
            ErrorType.Conflict => Error.Conflict(code, desc),
            _ => Error.Failure(code, desc),
        };

        // Assert
        error.Code.Should().Be(code);
        error.Description.Should().Be(desc);
        error.Type.Should().Be(type);
    }
}
