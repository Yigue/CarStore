using Application.Abstractions.Behaviors;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using SharedKernel;

namespace BehaviorTests;

public sealed record LoggingTestRequest;

public class RequestLoggingPipelineBehaviorTests
{
    [Fact]
    public async Task Handle_ShouldLogInformation_OnSuccess()
    {
        var logger = new Mock<ILogger<RequestLoggingPipelineBehavior<LoggingTestRequest, Result>>>();
        var behavior = new RequestLoggingPipelineBehavior<LoggingTestRequest, Result>(logger.Object);
        var next = new Mock<RequestHandlerDelegate<Result>>();
        next.Setup(n => n()).ReturnsAsync(Result.Success());

        await behavior.Handle(new LoggingTestRequest(), next.Object, CancellationToken.None);

        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() == "Processing request LoggingTestRequest"),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);

        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() == "Completed request LoggingTestRequest"),
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
        var logger = new Mock<ILogger<RequestLoggingPipelineBehavior<LoggingTestRequest, Result>>>();
        var behavior = new RequestLoggingPipelineBehavior<LoggingTestRequest, Result>(logger.Object);
        var next = new Mock<RequestHandlerDelegate<Result>>();
        next.Setup(n => n()).ReturnsAsync(Result.Failure(Error.Failure("Error", "message")));

        await behavior.Handle(new LoggingTestRequest(), next.Object, CancellationToken.None);

        logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() == "Completed request LoggingTestRequest with error"),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }
}
