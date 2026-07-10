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
}
