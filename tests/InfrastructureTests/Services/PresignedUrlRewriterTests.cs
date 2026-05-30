using System;
using FluentAssertions;
using Infrastructure.Services.Internal;
using Xunit;

namespace InfrastructureTests.Services;

public class PresignedUrlRewriterTests
{
    [Fact]
    public void Rewrite_ReplacesInternalHostWithPublicHost_AndKeepsQueryString()
    {
        var presigned = new Uri("http://minio:9000/cars/d-1/c-1/x.jpg?X-Amz-Signature=abc&X-Amz-Expires=900");
        var publicEndpoint = new Uri("http://localhost:9000");

        Uri result = PresignedUrlRewriter.Rewrite(presigned, publicEndpoint);

        result.Host.Should().Be("localhost");
        result.Port.Should().Be(9000);
        result.AbsolutePath.Should().Be("/cars/d-1/c-1/x.jpg");
        result.Query.Should().Be("?X-Amz-Signature=abc&X-Amz-Expires=900");
        result.ToString().Should().NotContain("minio:9000");
    }

    [Fact]
    public void Rewrite_StringOverload_NormalizesSchemelessEndpoint()
    {
        var presigned = new Uri("http://minio:9000/cars/x.jpg?sig=1");

        Uri result = PresignedUrlRewriter.Rewrite(presigned, "https://cdn.example.com");

        result.Scheme.Should().Be("https");
        result.Host.Should().Be("cdn.example.com");
        result.ToString().Should().NotContain("minio:9000");
        result.Query.Should().Be("?sig=1");
    }

    [Fact]
    public void Rewrite_PublicEndpointWithDefaultPort_DropsExplicitPort()
    {
        var presigned = new Uri("http://minio:9000/cars/x.jpg?sig=1");

        Uri result = PresignedUrlRewriter.Rewrite(presigned, new Uri("https://images.carstore.com"));

        result.IsDefaultPort.Should().BeTrue();
        result.ToString().Should().StartWith("https://images.carstore.com/cars/x.jpg");
        result.ToString().Should().NotContain(":9000");
    }

    [Fact]
    public void NormalizeEndpoint_AddsHttpScheme_WhenMissing()
    {
        Uri result = PresignedUrlRewriter.NormalizeEndpoint("minio:9000");

        result.Scheme.Should().Be("http");
        result.Host.Should().Be("minio");
        result.Port.Should().Be(9000);
    }
}
