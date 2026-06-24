namespace Web.Api.Endpoints.Users;

internal static class Permissions
{
    internal const string UsersAccess = "users:access";
    internal const string UsersCreate = "users:create";

    // RBAC permissions for user management
    internal const string CanManageUsers = "CanManageUsers";
    internal const string CanManageRoles = "CanManageRoles";
    internal const string CanManageInventory = "CanManageInventory";
    internal const string CanManageSales = "CanManageSales";
    internal const string CanManageFinance = "CanManageFinance";
    internal const string CanManageLeads = "CanManageLeads";
    internal const string CanViewReports = "CanViewReports";
    internal const string CanManageSettings = "CanManageSettings";
}