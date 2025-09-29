using Application.Abstractions.Behaviors;
using Microsoft.Extensions.Logging;
using Moq;
using SharedKernel;

namespace BehaviorTests;

public class RequestLoggingPipelineBehaviorTests
{
    private sealed record TestRequest;

    [Fact]
    public async Task Handle_ShouldLogInformation_OnSuccess()
    {
        var logger = new Mock<ILogger<RequestLoggingPipelineBehavior<TestRequest, Result>>>();
        var behavior = new RequestLoggingPipelineBehavior<TestRequest, Result>(logger.Object);
        var next = new Mock<RequestHandlerDelegate<Result>>();
        next.Setup(n => n()).ReturnsAsync(Result.Success());

        await behavior.Handle(new TestRequest(), next.Object, CancellationToken.None);

        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() == "Processing request TestRequest"),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);

        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() == "Completed request TestRequest"),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);

        logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldLogError_OnFailure()
    {
        var logger = new Mock<ILogger<RequestLoggingPipelineBehavior<TestRequest, Result>>>();
        var behavior = new RequestLoggingPipelineBehavior<TestRequest, Result>(logger.Object);
        var next = new Mock<RequestHandlerDelegate<Result>>();
        next.Setup(n => n()).ReturnsAsync(Result.Failure(Error.Failure("Error", "message")));

        await behavior.Handle(new TestRequest(), next.Object, CancellationToken.None);

        logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() == "Completed request TestRequest with error"),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }
}
