using Application.Abstractions.Behaviors;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Moq;
using SharedKernel;
using FluentAssertions;

namespace BehaviorTests;

public sealed record ValidationTestRequest;
public sealed record ValidationTestResponse;

public class ValidationPipelineBehaviorTests
{
    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenErrors()
    {
        var failure = new ValidationFailure("Name", "Required") { ErrorCode = "Name" };
        var validationResult = new ValidationResult([failure]);

        var validator = new Mock<IValidator<ValidationTestRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<ValidationTestRequest>>(), default))
            .ReturnsAsync(validationResult);

        var behavior = new ValidationPipelineBehavior<ValidationTestRequest, Result<ValidationTestResponse>>([validator.Object]);

        var next = new Mock<RequestHandlerDelegate<Result<ValidationTestResponse>>>();

        Result<ValidationTestResponse> result = await behavior.Handle(new ValidationTestRequest(), next.Object, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ValidationError>();
        next.Verify(n => n(), Times.Never);
    }
}
