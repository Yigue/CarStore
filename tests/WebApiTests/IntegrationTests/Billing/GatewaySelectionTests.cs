using Application.Abstractions.Billing;
using Infrastructure;
using Infrastructure.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using Xunit;

namespace WebApiTests.IntegrationTests.Billing;

public class GatewaySelectionTests
{
    private sealed class DummyPublisher : MediatR.IPublisher
    {
        public System.Threading.Tasks.Task Publish(object notification, System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task Publish<TNotification>(TNotification notification, System.Threading.CancellationToken cancellationToken = default) where TNotification : MediatR.INotification => System.Threading.Tasks.Task.CompletedTask;
    }

    private sealed class DummyHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Web.Api";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Theory]
    [InlineData("false", "sk_test_dummy", typeof(NoOpSubscriptionGateway))]
    [InlineData(null, "", typeof(NoOpSubscriptionGateway))]
    [InlineData("true", "sk_test_dummy", typeof(StripeSubscriptionGateway))]
    [InlineData(null, "sk_test_real_key", typeof(StripeSubscriptionGateway))]
    public void GatewaySelection_ResolvesExpectedGatewayType(string? stripeEnabled, string? secretKey, Type expectedGatewayType)
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "UseInMemoryDatabase", "true" },
            { "Jwt:Secret", "SecretKeyForTestingPurposesOnly1234567890" },
            { "Storage:Minio:InternalEndpoint", "http://minio:9000" },
            { "Storage:Minio:PublicEndpoint", "http://localhost:9000" },
            { "Storage:Minio:AccessKey", "minioadmin" },
            { "Storage:Minio:SecretKey", "minioadmin123" },
            { "Storage:Minio:BucketName", "cars" },
            { "Stripe:WebhookSecret", "whsec_test" },
            { "Stripe:PriceId", "price_test" }
        };
        if (stripeEnabled != null)
        {
            inMemorySettings["Stripe:Enabled"] = stripeEnabled;
        }
        if (secretKey != null)
        {
            inMemorySettings["Stripe:SecretKey"] = secretKey;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment, DummyHostEnvironment>();
        services.AddScoped<MediatR.IPublisher, DummyPublisher>();
        services.AddDbContext<Infrastructure.Database.ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid()));
        services.AddScoped<Application.Abstractions.Data.IApplicationDbContext>(sp =>
            sp.GetRequiredService<Infrastructure.Database.ApplicationDbContext>());
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var gateway = provider.GetRequiredService<ISubscriptionGateway>();

        Assert.IsType(expectedGatewayType, gateway);
    }
}
