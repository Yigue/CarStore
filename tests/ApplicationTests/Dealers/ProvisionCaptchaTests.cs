using Application.Abstractions.Data;
using Application.Abstractions.Storage;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApplicationTests.Dealers;

/// <summary>
/// spr PR 3.3 (B10) — task 3.3.1: proves the host refuses to boot in Production with the always-valid
/// <see cref="Infrastructure.Security.NoOpCaptchaVerifier"/> resolved as <c>ICaptchaVerifier</c>.
/// The HTTP-level "missing/invalid/unavailable CAPTCHA token" contract for
/// <c>POST dealers/provision</c> is covered in
/// <c>WebApiTests/Dealers/ProvisionEndpointTests.cs</c> (same project family, already wired to
/// <c>CustomWebApplicationFactory</c> and DB row assertions) — duplicating that factory wiring here
/// would not exercise anything new.
/// </summary>
public class ProvisionCaptchaTests
{
    [Fact]
    public void ProductionHost_WithoutCaptchaSecretKey_ThrowsInvalidOperationException()
    {
        // Arrange: production factory with Captcha:SecretKey left empty — AddCaptcha resolves
        // NoOpCaptchaVerifier, which the startup guard in Program.cs must refuse to boot with.
        using var factory = new CaptchaProductionFactory(captchaSecretKey: null);

        // Act & Assert: the host fails to start because the guard detects NoOpCaptchaVerifier.
        var act = () => factory.CreateClient();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Captcha*");
    }

    [Fact]
    public void ProductionHost_WithCaptchaSecretKeyConfigured_BootsSuccessfully()
    {
        // Arrange: production factory WITH Captcha:SecretKey set — AddCaptcha resolves
        // TurnstileCaptchaVerifier, so the NoOp guard must NOT fire.
        using var factory = new CaptchaProductionFactory(captchaSecretKey: "test-secret-key");

        // Act & Assert: boot succeeds (no InvalidOperationException from the captcha guard).
        var act = () => factory.CreateClient();
        act.Should().NotThrow();
    }

    /// <summary>
    /// WebApplicationFactory that boots the app in "Production" with the minimum settings the other
    /// non-captcha production fail-fast checks (Jwt:Secret length, Tenant:DevFallbackDealerId, Stripe
    /// webhook secret) already require — mirrors
    /// <c>WebApiTests/Platform/NoTenantServiceProductionGuardTests.cs</c>'s factory.
    /// </summary>
    private sealed class CaptchaProductionFactory : WebApplicationFactory<Web.Api.Program>
    {
        private readonly SqliteConnection _connection;
        private readonly string? _captchaSecretKey;

        public CaptchaProductionFactory(string? captchaSecretKey)
        {
            _captchaSecretKey = captchaSecretKey;
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Jwt:Secret", "SecretKeyForCaptchaProductionGuardTest1234567890");
            builder.UseSetting("Jwt:Issuer", "CarStore");
            builder.UseSetting("Jwt:Audience", "CarStore");
            builder.UseSetting("ConnectionStrings:Redis", "");
            builder.UseSetting("Storage:Minio:InternalEndpoint", "http://minio:9000");
            builder.UseSetting("Storage:Minio:PublicEndpoint", "http://localhost:9000");
            builder.UseSetting("Storage:Minio:AccessKey", "minioadmin");
            builder.UseSetting("Storage:Minio:SecretKey", "minioadmin123");
            builder.UseSetting("Storage:Minio:BucketName", "cars");
            builder.UseSetting("Stripe:SecretKey", "sk_test_dummy");
            builder.UseSetting("Stripe:PriceId", "price_dummy");
            builder.UseSetting("Stripe:WebhookSecret", "whsec_dummy");

            if (_captchaSecretKey is not null)
            {
                builder.UseSetting("Captcha:SecretKey", _captchaSecretKey);
            }

            builder.UseEnvironment("Production");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<IApplicationDbContext>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection).UseSnakeCaseNamingConvention());
                services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

                services.RemoveAll<IStorageService>();
                services.AddSingleton<IStorageService>(new NoOpStorageServiceStub());
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _connection.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>Minimal IStorageService double — provisioning's captcha guard never touches storage.</summary>
    private sealed class NoOpStorageServiceStub : IStorageService
    {
        public Task<string> UploadFileAsync(Stream stream, string objectKey, string contentType, long? size, CancellationToken ct) =>
            Task.FromResult(objectKey);

        public Task<(string Url, IReadOnlyDictionary<string, string> Fields)> GeneratePresignedPostAsync(
            string objectKey, string contentType, TimeSpan ttl, CancellationToken ct) =>
            Task.FromResult<(string Url, IReadOnlyDictionary<string, string> Fields)>(("http://noop/x", new Dictionary<string, string>()));

        public Task DeleteFileAsync(string objectKey, CancellationToken ct) => Task.CompletedTask;

        public Task<Stream?> OpenReadAsync(string objectKey, CancellationToken ct) => Task.FromResult<Stream?>(null);

        public Task<byte[]?> ReadHeadAsync(string objectKey, int count, CancellationToken ct) => Task.FromResult<byte[]?>(null);

        public Task<long?> GetObjectSizeAsync(string objectKey, CancellationToken ct) => Task.FromResult<long?>(null);

        public Task<Uri> GetPresignedUrlAsync(string objectKey, TimeSpan ttl, CancellationToken ct) => Task.FromResult(new Uri("http://noop/x"));
    }
}
