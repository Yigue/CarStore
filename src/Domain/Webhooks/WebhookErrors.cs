using SharedKernel;

namespace Domain.Webhooks;

public static class WebhookErrors
{
    public static Error NotFound(Guid subscriptionId) => Error.NotFound(
        "Webhooks.NotFound",
        $"The webhook subscription with the Id = '{subscriptionId}' was not found.");
}
