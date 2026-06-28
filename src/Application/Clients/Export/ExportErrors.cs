using SharedKernel;

namespace Application.Clients.Export;

public static class ExportErrors
{
    public const int MaxRows = 10_000;

    public static Error LimitExceeded(int selected) => Error.Problem(
        "Export.LimitExceeded",
        $"Export would include {selected} rows, which exceeds the maximum of {MaxRows}.");
}
