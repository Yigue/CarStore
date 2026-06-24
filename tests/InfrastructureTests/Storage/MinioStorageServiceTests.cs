using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace InfrastructureTests.Storage;

/// <summary>
/// Regression for the presigned POST upload (browser -> MinIO). The MinIO SDK's
/// <c>PresignedPostPolicyAsync</c> records a <c>$Content-Type</c> policy condition when a content
/// type is set, but does NOT emit a matching <c>Content-Type</c> form field. If the field is
/// missing, the browser POST violates the policy and MinIO answers 403 "Policy Condition failed".
/// The service must therefore return <c>Content-Type</c> among the form fields.
/// Signing is local (no network/Docker), so a throwaway endpoint is fine.
/// </summary>
public class MinioStorageServiceTests
{
    private static MinioStorageService CreateService() =>
        new(
            Options.Create(new MinioOptions
            {
                InternalEndpoint = "http://localhost:9000",
                PublicEndpoint = "http://localhost:9000",
                AccessKey = "minioadmin",
                SecretKey = "minioadmin123",
                BucketName = "cars",
                Region = "us-east-1",
            }),
            NullLogger<MinioStorageService>.Instance);

    [Fact]
    public async Task GeneratePresignedPostAsync_IncludesContentTypeField_MatchingThePolicy()
    {
        MinioStorageService service = CreateService();

        (string _, var fields) = await service.GeneratePresignedPostAsync(
            "11111111-1111-1111-1111-111111111111/car-1/img-1.jpg",
            "image/jpeg",
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        fields.Should().ContainKey("Content-Type");
        fields["Content-Type"].Should().Be("image/jpeg");
    }
}
