using Application.Abstractions.Behaviors;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using SharedKernel;
using FluentAssertions;

namespace BehaviorTests;

public class ValidationPipelineBehaviorTests
{
    private sealed record TestRequest;
    private sealed record TestResponse;

    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenErrors()
    {
        var failure = new ValidationFailure("Name", "Required") { ErrorCode = "Name" };
        var validationResult = new ValidationResult([failure]);

        var validator = new Mock<IValidator<TestRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), default))
            .ReturnsAsync(validationResult);

        var behavior = new ValidationPipelineBehavior<TestRequest, Result<TestResponse>>([validator.Object]);

        var next = new Mock<RequestHandlerDelegate<Result<TestResponse>>>();

        Result<TestResponse> result = await behavior.Handle(new TestRequest(), next.Object, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ValidationError>();
        next.Verify(n => n(), Times.Never);
    }
}
