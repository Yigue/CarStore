using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Billing.EventHandlers;
using Application.UnitTests;
using Domain.Billing.Events;
using Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ApplicationTests.Billing;

public class SendPaymentFailedEmailHandlerTests
{
    private readonly TestApplicationDbContext _dbContext;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly SendPaymentFailedEmailHandler _handler;

    public SendPaymentFailedEmailHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new TestApplicationDbContext(options);

        _emailServiceMock = new Mock<IEmailService>();
        _handler = new SendPaymentFailedEmailHandler(_dbContext, _emailServiceMock.Object);
    }

    [Fact]
    public async Task Handle_SendsEmailToAdmin_WhenPaymentFails()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var adminUser = new User(dealerId, "admin@dealer.com", "John", "Doe", "hash", Guid.NewGuid());
        _dbContext.Users.Add(adminUser);
        await _dbContext.SaveChangesAsync();

        var notification = new SubscriptionPaymentFailedDomainEvent(Guid.NewGuid(), dealerId);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            "admin@dealer.com",
            It.Is<string>(s => s.Contains("Payment Failed")),
            It.Is<string>(b => b.Contains("failed") && b.Contains("John")),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }
}
