using Application.Abstractions.Messaging;
using Infrastructure;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InfrastructureTests.Email;

public class EmailServiceDiFactoryTests
{
    private static IServiceProvider BuildProvider(Dictionary<string, string?> configValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();

        // Wire only the email-related portion of AddInfrastructure
        // by calling the full AddInfrastructure but with UseInMemoryDatabase to skip DB
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AbsentSmtpConfig_ResolvesNoOpEmailService()
    {
        // Arrange — no Email:Smtp:Host in config
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["UseInMemoryDatabase"] = "true",
            ["Jwt:Secret"] = "supersecretkeysupersecretkeysupersecretkey",
            ["Jwt:Issuer"] = "test",
            ["Jwt:Audience"] = "test"
        });

        // Act
        var emailService = provider.GetRequiredService<IEmailService>();

        // Assert
        emailService.Should().BeOfType<NoOpEmailService>();
    }

    [Fact]
    public void PresentSmtpHost_ResolvesSmtpEmailService()
    {
        // Arrange — Email:Smtp:Host and Email:Smtp:FromAddress present
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["UseInMemoryDatabase"] = "true",
            ["Jwt:Secret"] = "supersecretkeysupersecretkeysupersecretkey",
            ["Jwt:Issuer"] = "test",
            ["Jwt:Audience"] = "test",
            ["Email:Smtp:Host"] = "localhost",
            ["Email:Smtp:FromAddress"] = "x@y.com",
            ["Email:Smtp:Port"] = "1025",
            ["Email:Smtp:UseStartTls"] = "false"
        });

        // Act
        var emailService = provider.GetRequiredService<IEmailService>();

        // Assert
        emailService.Should().BeOfType<SmtpEmailService>();
    }

    [Fact]
    public void PresentResendApiKey_ResolvesResendEmailService()
    {
        // Arrange — Email:Resend:ApiKey present
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["UseInMemoryDatabase"] = "true",
            ["Jwt:Secret"] = "supersecretkeysupersecretkeysupersecretkey",
            ["Jwt:Issuer"] = "test",
            ["Jwt:Audience"] = "test",
            ["Email:Resend:ApiKey"] = "re_123456789"
        });

        // Act
        var emailService = provider.GetRequiredService<IEmailService>();

        // Assert
        emailService.Should().BeOfType<ResendEmailService>();
    }

    [Fact]
    public async Task NoOp_SendEmailAsync_DoesNotThrow()
    {
        // Arrange
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["UseInMemoryDatabase"] = "true",
            ["Jwt:Secret"] = "supersecretkeysupersecretkeysupersecretkey",
            ["Jwt:Issuer"] = "test",
            ["Jwt:Audience"] = "test"
        });

        var emailService = provider.GetRequiredService<IEmailService>();

        // Act
        Func<Task> act = () => emailService.SendEmailAsync(
            "a@b.com", "subject", "body", CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
