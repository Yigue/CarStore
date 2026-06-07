namespace Web.Api.Endpoints.Cars;

internal static class Permissions
{
    internal const string CarsCreate = "cars:create";
    internal const string CarsRead = "cars:read";
    internal const string CarsUpdate = "cars:update";
    internal const string CarsDelete = "cars:delete";

    /// <summary>Admin-only: repair legacy car_images rows after the object-storage migration.</summary>
    internal const string AdminBackfill = "admin:backfill";
}

