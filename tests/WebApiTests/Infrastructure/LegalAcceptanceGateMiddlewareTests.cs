using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Application.Legal;
using Infrastructure.Caching;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Web.Api.Infrastructure;
using Xunit;

// Not WebApiTests.Infrastructure: that namespace would shadow the Infrastructure assembly in sibling tests.
namespace WebApiTests.LegalGate;

public class LegalAcceptanceGateMiddlewareTests
{
    private const string CurrentVersion = "2026-10-01";

    private sealed record Harness(DefaultHttpContext Context, Mock<ICacheService> Cache);

    private static Harness Build(
        string method,
        string path,
        string? tokenVersion = null,
        bool authenticated = true,
        bool material = true,
        string? cachedVersion = null,
        Guid? userId = null)
    {
        Guid uid = userId ?? Guid.NewGuid();

        var terms = new Mock<ILegalTermsProvider>();
        terms.SetupGet(t => t.Current).Returns(new LegalTermsInfo(CurrentVersion, "abc", material));

        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetAsync<string>(LegalAcceptanceGateMiddleware.CacheKey(uid), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(cachedVersion);

        var services = new ServiceCollection();
        services.AddScoped(_ => terms.Object);
        services.AddScoped(_ => cache.Object);

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() }
        };
        context.Request.Method = method;
        context.Request.Path = path;

        if (authenticated)
        {
            var claims = new System.Collections.Generic.List<Claim> { new("sub", uid.ToString()) };
            if (tokenVersion is not null)
            {
                claims.Add(new Claim("tv", tokenVersion));
            }

            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test", "sub", "role"));
        }

        return new Harness(context, cache);
    }

    private static async Task<(bool nextCalled, int status, string body)> Run(Harness h)
    {
        bool nextCalled = false;
        var middleware = new LegalAcceptanceGateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(h.Context);
        h.Context.Response.Body.Seek(0, SeekOrigin.Begin);
        string body = await new StreamReader(h.Context.Response.Body).ReadToEndAsync();
        return (nextCalled, h.Context.Response.StatusCode, body);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Write_WithoutCurrentAcceptance_Returns428(string method)
    {
        Harness h = Build(method, "/api/v1/cars");

        (bool next, int status, string body) = await Run(h);

        Assert.False(next);
        Assert.Equal(StatusCodes.Status428PreconditionRequired, status);
        Assert.Contains("legal.acceptance_required", body);
        Assert.Contains(CurrentVersion, body);
    }

    [Fact]
    public async Task Write_WithStaleTokenVersionAndNoCache_Returns428()
    {
        Harness h = Build("POST", "/api/v1/cars", tokenVersion: "2026-01-01");

        (bool next, int status, _) = await Run(h);

        Assert.False(next);
        Assert.Equal(StatusCodes.Status428PreconditionRequired, status);
    }

    [Fact]
    public async Task Write_WithCurrentTokenClaim_CallsNext()
    {
        Harness h = Build("POST", "/api/v1/cars", tokenVersion: CurrentVersion);

        (bool next, int status, _) = await Run(h);

        Assert.True(next);
        Assert.NotEqual(StatusCodes.Status428PreconditionRequired, status);
    }

    [Fact]
    public async Task Write_WithStaleClaimButCurrentCache_CallsNext()
    {
        Harness h = Build("POST", "/api/v1/cars", tokenVersion: "2026-01-01", cachedVersion: CurrentVersion);

        (bool next, _, _) = await Run(h);

        Assert.True(next);
    }

    [Fact]
    public async Task Write_WithoutClaimButCurrentCache_CallsNext()
    {
        Harness h = Build("POST", "/api/v1/cars", cachedVersion: CurrentVersion);

        (bool next, _, _) = await Run(h);

        Assert.True(next);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task SafeMethods_AreNeverBlocked(string method)
    {
        Harness h = Build(method, "/api/v1/cars");

        (bool next, _, _) = await Run(h);

        Assert.True(next);
        h.Cache.Verify(c => c.GetAsync<string>(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("POST", "/api/v1/legal/acceptances")]
    [InlineData("POST", "/api/v1/users/logout")]
    [InlineData("POST", "/api/v1/users/me")]
    [InlineData("PUT", "/api/v1/users/me")]
    public async Task ExemptPaths_AreNeverBlocked(string method, string path)
    {
        Harness h = Build(method, path);

        (bool next, _, _) = await Run(h);

        Assert.True(next);
    }

    [Fact]
    public async Task NonMaterialVersion_DoesNotBlock()
    {
        Harness h = Build("POST", "/api/v1/cars", material: false);

        (bool next, _, _) = await Run(h);

        Assert.True(next);
    }

    [Fact]
    public async Task AnonymousWrite_IsNotTheGatesConcern()
    {
        Harness h = Build("POST", "/api/v1/users/login", authenticated: false);

        (bool next, _, _) = await Run(h);

        Assert.True(next);
    }
}
