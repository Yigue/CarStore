using SharedKernel;

public class ResultTests
{
    [Fact]
    public void Success_ReturnsSuccessResult()
    {
        // Act
        var result = Result.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_ReturnsFailureResultWithError()
    {
        // Arrange
        var error = Error.Failure("code", "desc");

        // Act
        var result = Result.Failure(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void ImplicitConversion_NullValue_ReturnsFailure()
    {
        // Act
        Result<string> result = null!;

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.NullValue);
    }

    [Fact]
    public void AccessingValueOnFailure_ThrowsInvalidOperationException()
    {
        // Arrange
        var result = Result.Failure<string>(Error.Failure("code", "desc"));

        // Act
        Action act = () => _ = result.Value;

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
