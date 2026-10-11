using System;
using System.IO;
using System.Threading.Tasks;
using Application.Abstractions.Billing;
using Application.Abstractions.Tenancy;
using Domain.Billing;
using Domain.DealerSettings;
using Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace WebApiTests.IntegrationTests.Billing;

/// <summary>RN-013 (A6, H9): the guard consults the method-sensitive access policy instead of a binary check.</summary>
public class SubscriptionGuardLifecycleTests
{
    private static (DefaultHttpContext Context, Func<bool> NextCalled, SubscriptionGuardMiddleware Middleware, Mock<ITenantLifecycleReader> Reader)
        Arrange(SubscriptionStatus? status, TenantLifecycleStatus lifecycle, string method, string path)
    {
        var dealerId = Guid.NewGuid();
        var cache = new Mock<ISubscriptionStatusCache>();
        cache.Setup(c => c.GetAsync(dealerId, default)).ReturnsAsync(status);
        var reader = new Mock<ITenantLifecycleReader>();
        reader.Setup(r => r.GetAsync(dealerId, default)).ReturnsAsync(lifecycle);

        var services = new ServiceCollection();
        services.AddScoped(_ => cache.Object);
        services.AddScoped(_ => reader.Object);

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
        return (context, () => nextCalled, middleware, reader);
    }

    private static async Task<string> BodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await new StreamReader(context.Response.Body).ReadToEndAsync();
    }

    [Theory]
    [InlineData("GET", "/api/v1/clients")]
    [InlineData("HEAD", "/api/v1/clients")]
    [InlineData("POST", "/api/v1/dealer/exports")]
    public async Task CancelledGrace_ReadsAndExportPassThrough(string method, string path)
    {
        var (context, nextCalled, middleware, _) =
            Arrange(SubscriptionStatus.Cancelled, TenantLifecycleStatus.CancelledGrace, method, path);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled());
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task CancelledGrace_WritesGet402ReadOnly(string method)
    {
        var (context, nextCalled, middleware, _) =
            Arrange(SubscriptionStatus.Cancelled, TenantLifecycleStatus.CancelledGrace, method, "/api/v1/clients");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled());
        Assert.Equal(StatusCodes.Status402PaymentRequired, context.Response.StatusCode);
        Assert.Contains("subscription.read_only", await BodyAsync(context));
    }

    [Fact]
    public async Task Locked_BlocksReads_With402Locked()
    {
        var (context, nextCalled, middleware, _) =
            Arrange(SubscriptionStatus.Cancelled, TenantLifecycleStatus.Locked, "GET", "/api/v1/clients");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled());
        Assert.Equal(StatusCodes.Status402PaymentRequired, context.Response.StatusCode);
        Assert.Contains("subscription.locked", await BodyAsync(context));
    }

    [Fact]
    public async Task Locked_AllowsExportDownload()
    {
        var (context, nextCalled, middleware, _) = Arrange(
            SubscriptionStatus.Cancelled, TenantLifecycleStatus.Locked, "GET",
            "/api/v1/dealer/exports/3f2b8f0e-1111-2222-3333-444455556666/download");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled());
    }

    [Fact]
    public async Task ActiveSubscription_DoesNotQueryTheLifecycle()
    {
        var (context, nextCalled, middleware, reader) =
            Arrange(SubscriptionStatus.Active, TenantLifecycleStatus.Active, "POST", "/api/v1/clients");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled());
        reader.Verify(r => r.GetAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task Suspended_WithActiveLifecycle_KeepsSuspendedError()
    {
        var (context, nextCalled, middleware, _) =
            Arrange(SubscriptionStatus.Suspended, TenantLifecycleStatus.Active, "GET", "/api/v1/clients");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled());
        Assert.Contains("subscription.suspended", await BodyAsync(context));
    }
}
