using Application.Abstractions.Billing;
using Domain.Billing;
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ApplicationTests.Billing;

public class DealerSubscriptionRepositoryTests
{
    [Fact]
    public async Task RepositoryMock_ShouldPerformOperations()
    {
        var mockRepo = new Mock<IDealerSubscriptionRepository>();
        var dealerId = Guid.NewGuid();
        var subscription = DealerSubscription.Create(dealerId, "cus_123", "sub_123", "plan_123");

        mockRepo.Setup(r => r.GetByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var result = await mockRepo.Object.GetByDealerIdAsync(dealerId);

        result.Should().NotBeNull();
        result.DealerId.Should().Be(dealerId);
        result.StripeCustomerId.Should().Be("cus_123");

        mockRepo.Verify(r => r.GetByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
