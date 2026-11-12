using Application.Abstractions.Billing;
using Application.Abstractions.Data;
using Application.Abstractions.Tenancy;
using Application.UnitTests;
using Domain.Billing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Web.Api.Infrastructure;

namespace Application.UnitTests.Billing;

/// <summary>sfc A3.2 (A2, A24, RN-020, RN-021): a feature not included in the plan is a 403 <c>plan.feature_unavailable</c>.</summary>
public class RequireFeatureFilterTests
{
    private static TestApplicationDbContext CreateContext(Guid dealerId)
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options, dealerId);
    }

    private static HttpContext BuildHttpContext(Guid dealerId, TestApplicationDbContext context, IPlanCatalog planCatalog)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<ICurrentTenantService>(t => t.DealerId == dealerId && t.HasTenant == true));
        services.AddSingleton<IApplicationDbContext>(context);
        services.AddSingleton(planCatalog);

        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }

    [Fact]
    public async Task InvokeAsync_WhenFeatureNotIncludedInThePlan_Returns403FeatureUnavailable()
    {
        var dealerId = Guid.NewGuid();
        var context = CreateContext(dealerId);
        context.DealerSubscriptions.Add(DealerSubscription.CreatePending(dealerId, null, planId: string.Empty, planCode: "standard"));
        await context.SaveChangesAsync();

        var planCatalog = new Mock<IPlanCatalog>();
        planCatalog.Setup(p => p.GetFeaturesAsync("standard", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string>)Array.Empty<string>());

        var httpContext = BuildHttpContext(dealerId, context, planCatalog.Object);
        var invocationContext = EndpointFilterInvocationContext.Create(httpContext);
        var filter = new RequireFeatureFilter("crm");

        object? result = await filter.InvokeAsync(invocationContext, _ => ValueTask.FromResult<object?>(Results.Ok()));

        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        problem.ProblemDetails.Title.Should().Be("plan.feature_unavailable");
    }

    [Fact]
    public async Task InvokeAsync_WhenFeatureIsIncludedInThePlan_CallsNext()
    {
        var dealerId = Guid.NewGuid();
        var context = CreateContext(dealerId);
        context.DealerSubscriptions.Add(DealerSubscription.CreatePending(dealerId, null, planId: string.Empty, planCode: "pro"));
        await context.SaveChangesAsync();

        var planCatalog = new Mock<IPlanCatalog>();
        planCatalog.Setup(p => p.GetFeaturesAsync("pro", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string>)new List<string> { "crm" });

        var httpContext = BuildHttpContext(dealerId, context, planCatalog.Object);
        var invocationContext = EndpointFilterInvocationContext.Create(httpContext);
        var filter = new RequireFeatureFilter("crm");
        bool nextCalled = false;

        object? result = await filter.InvokeAsync(invocationContext, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        });

        nextCalled.Should().BeTrue();
        result.Should().BeOfType<Ok>();
    }
}
