using SharedKernel;

namespace Application.Platform.Common;

public static class PlatformErrors
{
    public static readonly Error ETagMismatch = Error.Conflict(
        "Platform.ETagMismatch",
        "The provided ETag does not match the current resource version. Fetch the latest version and retry.");

    public static readonly Error DealerNotFound = Error.NotFound(
        "Platform.DealerNotFound",
        "The specified dealer settings were not found.");
}
