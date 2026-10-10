using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Application.Abstractions.Billing;
using Domain.Billing;
using Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace WebApiTests.IntegrationTests.Billing;

public class SubscriptionGuardMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SuspendedDealer_Returns402()
    {
        var dealerId = Guid.NewGuid();
        var cacheMock = new Mock<ISubscriptionStatusCache>();
        cacheMock.Setup(c => c.GetAsync(dealerId, default)).ReturnsAsync(SubscriptionStatus.Suspended);

        var services = new ServiceCollection();
        services.AddScoped(_ => cacheMock.Object);
        
        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() }
        };
        context.Items["Tenant.DealerId"] = dealerId;
        context.Request.Path = "/api/v1/some-endpoint";
        
        var nextCalled = false;
        var middleware = new SubscriptionGuardMiddleware(innerContext =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status402PaymentRequired, context.Response.StatusCode);
        
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("subscription.suspended", body);
    }

    [Fact]
    public async Task InvokeAsync_ActiveDealer_CallsNext()
    {
        var dealerId = Guid.NewGuid();
        var cacheMock = new Mock<ISubscriptionStatusCache>();
        cacheMock.Setup(c => c.GetAsync(dealerId, default)).ReturnsAsync(SubscriptionStatus.Active);

        var services = new ServiceCollection();
        services.AddScoped(_ => cacheMock.Object);
        
        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
        context.Items["Tenant.DealerId"] = dealerId;
        context.Request.Path = "/api/v1/some-endpoint";
        
        var nextCalled = false;
        var middleware = new SubscriptionGuardMiddleware(innerContext =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ExemptPath_CallsNextWithoutStatusCheck()
    {
        var dealerId = Guid.NewGuid();
        var cacheMock = new Mock<ISubscriptionStatusCache>();
        
        var services = new ServiceCollection();
        services.AddScoped(_ => cacheMock.Object);
        
        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
        context.Items["Tenant.DealerId"] = dealerId;
        context.Request.Path = "/api/v1/subscriptions/status";
        
        var nextCalled = false;
        var middleware = new SubscriptionGuardMiddleware(innerContext =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        cacheMock.Verify(c => c.GetAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_AnonymousNonCatalog_CallsNextWithoutStatusCheck()
    {
        var cacheMock = new Mock<ISubscriptionStatusCache>();
        var services = new ServiceCollection();
        services.AddScoped(_ => cacheMock.Object);
        
        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
        // No Tenant.DealerId in items -> HasTenant = false
        context.Request.Path = "/api/v1/some-endpoint";
        
        var nextCalled = false;
        var middleware = new SubscriptionGuardMiddleware(innerContext =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        cacheMock.Verify(c => c.GetAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    private static (DefaultHttpContext Context, Mock<ISubscriptionStatusCache> Cache, Func<bool> NextCalled, SubscriptionGuardMiddleware Middleware)
        Arrange(Guid dealerId, SubscriptionStatus? status, string path, string method = "GET")
    {
        var cacheMock = new Mock<ISubscriptionStatusCache>();
        cacheMock.Setup(c => c.GetAsync(dealerId, default)).ReturnsAsync(status);

        var services = new ServiceCollection();
        services.AddScoped(_ => cacheMock.Object);

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() }
        };
        context.Items["Tenant.DealerId"] = dealerId;
        context.Request.Path = path;
        context.Request.Method = method;

        var nextCalled = false;
        var middleware = new SubscriptionGuardMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        return (context, cacheMock, () => nextCalled, middleware);
    }

    // spr ADR B8 (task 3.1.3): the legacy dealer with no subscription row used to pass (fail-open).
    [Fact]
    public async Task InvokeAsync_NoSubscriptionRow_Returns402()
    {
        var (context, _, nextCalled, middleware) = Arrange(Guid.NewGuid(), null, "/api/v1/clients");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled());
        Assert.Equal(StatusCodes.Status402PaymentRequired, context.Response.StatusCode);
    }

    // spr ADR B8 (task 3.1.2): a freshly provisioned dealer (Incomplete) does not use the product for free.
    [Fact]
    public async Task InvokeAsync_IncompleteDealer_Returns402()
    {
        var (context, _, nextCalled, middleware) = Arrange(Guid.NewGuid(), SubscriptionStatus.Incomplete, "/api/v1/clients");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled());
        Assert.Equal(StatusCodes.Status402PaymentRequired, context.Response.StatusCode);
    }

    // spr ADR B8: the dealer must be able to log in, read its status and start a checkout to pay its way out.
    [Theory]
    [InlineData("/api/v1/subscriptions/status", "GET")]
    [InlineData("/api/v1/subscriptions/checkout", "POST")]
    [InlineData("/api/v1/users/login", "POST")]
    [InlineData("/api/v1/users/forgot-password", "POST")]
    [InlineData("/api/v1/users/reset-password", "POST")]
    [InlineData("/api/v1/users/me", "GET")]
    public async Task InvokeAsync_IncompleteDealer_CanReachTheRoutesNeededToPay(string path, string method)
    {
        var (context, cache, nextCalled, middleware) = Arrange(Guid.NewGuid(), SubscriptionStatus.Incomplete, path, method);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled());
        cache.Verify(c => c.GetAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_NoSubscriptionRow_StillReachesStatusAndCheckout()
    {
        var (context, _, nextCalled, middleware) = Arrange(Guid.NewGuid(), null, "/api/v1/subscriptions/checkout", "POST");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled());
    }

    [Fact]
    public async Task InvokeAsync_CancelledDealer_Returns402()
    {
        var dealerId = Guid.NewGuid();
        var cacheMock = new Mock<ISubscriptionStatusCache>();
        cacheMock.Setup(c => c.GetAsync(dealerId, default)).ReturnsAsync(SubscriptionStatus.Cancelled);

        var services = new ServiceCollection();
        services.AddScoped(_ => cacheMock.Object);

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() }
        };
        context.Items["Tenant.DealerId"] = dealerId;
        context.Request.Path = "/api/v1/some-endpoint";

        var nextCalled = false;
        var middleware = new SubscriptionGuardMiddleware(innerContext =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status402PaymentRequired, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_AnonymousRequest_DoesNotLeakDealerIdOrStatus()
    {
        var dealerId = Guid.NewGuid();
        var cacheMock = new Mock<ISubscriptionStatusCache>();
        cacheMock.Setup(c => c.GetAsync(dealerId, default)).ReturnsAsync(SubscriptionStatus.Suspended);

        var services = new ServiceCollection();
        services.AddScoped(_ => cacheMock.Object);

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() }
        };
        context.Items["Tenant.DealerId"] = dealerId;
        context.Request.Path = "/api/v1/some-endpoint";

        var middleware = new SubscriptionGuardMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status402PaymentRequired, context.Response.StatusCode);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain(dealerId.ToString(), body);
        Assert.DoesNotContain("subscriptionStatus", body);
    }
}
