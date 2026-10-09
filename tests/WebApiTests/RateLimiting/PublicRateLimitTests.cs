using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApiTests.RateLimiting;

/// <summary>
/// API-007: public endpoints are rate limited per source IP (not one global bucket).
/// TestServer has no remote address, so a start-up filter lets each request choose its
/// own through the <c>X-Test-Remote-Ip</c> header.
/// </summary>
public class PublicRateLimitTests : IDisposable
{
    private const int PermitLimit = 2;
    private readonly CustomWebApplicationFactory _root = new();
    private readonly HttpClient _client;

    public PublicRateLimitTests()
    {
        _root.SeedDatabase();
        _client = _root.WithWebHostBuilder(b =>
        {
            b.UseSetting("RateLimiting:Public:PermitLimit", PermitLimit.ToString());
            b.UseSetting("RateLimiting:Public:WindowSeconds", "60");
            b.ConfigureServices(s => s.AddTransient<IStartupFilter, FakeRemoteIpStartupFilter>());
        }).CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _root.Dispose();
    }

    private sealed class FakeRemoteIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, pipeline) =>
            {
                if (context.Request.Headers.TryGetValue("X-Test-Remote-Ip", out var ip)
                    && IPAddress.TryParse(ip.ToString(), out var parsed))
                {
                    context.Connection.RemoteIpAddress = parsed;
                }

                await pipeline();
            });
            next(app);
        };
    }

    private async Task<HttpStatusCode> PostAsync(string path, string ip, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Test-Remote-Ip", ip);
        using var response = await _client.SendAsync(request);
        return response.StatusCode;
    }

    private Task<HttpStatusCode> Register(string ip) =>
        PostAsync("/api/v1/users/register", ip, new { Email = "not-an-email", FirstName = "", LastName = "", Password = "x" });

    [Fact]
    public async Task Register_ExceedingLimit_FromOneIp_Returns429()
    {
        for (var i = 0; i < PermitLimit; i++)
        {
            (await Register("203.0.113.10")).Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        (await Register("203.0.113.10")).Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Register_OtherIp_IsNotAffectedByTheFirstIpsLimit()
    {
        for (var i = 0; i <= PermitLimit; i++)
        {
            await Register("203.0.113.20");
        }

        (await Register("203.0.113.20")).Should().Be(HttpStatusCode.TooManyRequests);
        (await Register("203.0.113.21")).Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Newsletter_ExceedingLimit_Returns429()
    {
        for (var i = 0; i < PermitLimit; i++)
        {
            await PostAsync("/api/v1/newsletter/subscribe", "203.0.113.30", new { email = "a@b.com" });
        }

        (await PostAsync("/api/v1/newsletter/subscribe", "203.0.113.30", new { email = "a@b.com" }))
            .Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Inquiry_ExceedingLimit_Returns429()
    {
        for (var i = 0; i < PermitLimit; i++)
        {
            await PostAsync("/api/v1/quotes/inquiry", "203.0.113.40", new { });
        }

        (await PostAsync("/api/v1/quotes/inquiry", "203.0.113.40", new { }))
            .Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Provisioning_ExceedingLimit_Returns429()
    {
        for (var i = 0; i < PermitLimit; i++)
        {
            await PostAsync("/api/v1/dealers/provision", "203.0.113.50", new { });
        }

        (await PostAsync("/api/v1/dealers/provision", "203.0.113.50", new { }))
            .Should().Be(HttpStatusCode.TooManyRequests);
    }
}
