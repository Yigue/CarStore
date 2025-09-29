using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Users.Register;

public interface IUserNotificationService
{
    Task SendWelcomeEmailAsync(Guid userId, CancellationToken cancellationToken = default);
}
