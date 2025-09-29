using Application.Users.Register;
using Domain.Users;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace DomainEventsTests;

public class UserEventsTests
{
    [Fact]
    public void Register_user_raises_UserRegisteredDomainEvent()
    {
        var user = new User("user@test.com", "John", "Doe", "hash");

        user.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserRegisteredDomainEvent>()
            .Which.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task UserRegisteredDomainEvent_is_handled_by_UserRegisteredDomainEventHandler()
    {
        var services = new ServiceCollection();
        var notificationService = new TestUserNotificationService();
        services.AddSingleton<IUserNotificationService>(notificationService);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(UserRegisteredDomainEventHandler).Assembly));
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var user = new User("user@test.com", "John", "Doe", "hash");
        foreach (var domainEvent in user.DomainEvents)
        {
            await mediator.Publish(domainEvent);
        }

        notificationService.NotifiedUserIds.Should().Contain(user.Id);
    }

    private sealed class TestUserNotificationService : IUserNotificationService
    {
        public List<Guid> NotifiedUserIds { get; } = new();

        public Task SendWelcomeEmailAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            NotifiedUserIds.Add(userId);
            return Task.CompletedTask;
        }
    }
}
