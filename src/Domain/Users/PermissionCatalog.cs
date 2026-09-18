namespace Domain.Users;

/// <summary>
/// CFG-03: every permission this API actually enforces, in one place.
///
/// <para>
/// There used to be two disconnected lists. The endpoints guard themselves with strings like
/// <c>cars:read</c> and <c>quotes:accept</c>; the screen that hands permissions out offered a
/// hardcoded set of seven invented names (<c>CanManageInventory</c>, <c>CanViewReports</c>…) that
/// no endpoint has ever checked. A dealership could tick every box on that screen and still have
/// a user who could not open the inventory, because nothing it offered corresponded to anything
/// the API asks for.
/// </para>
///
/// <para>
/// This catalogue is that correspondence. <c>GetPermissionsQueryHandler</c> serves it, role
/// editing validates against it, and <c>ArchitectureTests.PermissionCatalogTests</c> fails the
/// build if an endpoint guards itself with a permission that is not listed here — so the screen
/// cannot drift away from the rules again.
/// </para>
///
/// <para>
/// Platform permissions (<c>platform:*</c>) are deliberately absent: they belong to CarStore's own
/// staff operating the SaaS, not to a dealership configuring its roles, and offering them here
/// would invite an administrator to grant an access their tenancy cannot contain.
/// </para>
/// </summary>
public static class PermissionCatalog
{
    /// <summary>A permission a dealership can grant, with the label its people read.</summary>
    /// <param name="Value">The exact string the endpoints check.</param>
    /// <param name="Module">Groups the matrix into the sections of the product.</param>
    /// <param name="Label">What it means, in the dealership's own language.</param>
    public sealed record PermissionDefinition(string Value, string Module, string Label);

    public const string ModuleInventory = "Inventario";
    public const string ModuleCrm = "CRM";
    public const string ModuleQuotes = "Cotizaciones";
    public const string ModuleSales = "Ventas";
    public const string ModuleFinance = "Finanzas";
    public const string ModuleAgenda = "Agenda";
    public const string ModuleDocuments = "Documentación";
    public const string ModuleAdministration = "Administración";

    private static readonly PermissionDefinition[] Definitions =
    [
        // ── Inventario ───────────────────────────────────────────────────────────────────────
        new("cars:read", ModuleInventory, "Ver el inventario"),
        new("cars:create", ModuleInventory, "Cargar vehículos"),
        new("cars:update", ModuleInventory, "Editar vehículos"),
        new("cars:delete", ModuleInventory, "Dar de baja vehículos"),

        // ── CRM ──────────────────────────────────────────────────────────────────────────────
        new("leads:read", ModuleCrm, "Ver los leads"),
        new("leads:write", ModuleCrm, "Crear y mover leads en el pipeline"),
        new("leads:archive", ModuleCrm, "Archivar leads"),
        new("clients:read", ModuleCrm, "Ver los clientes"),
        new("clients:write", ModuleCrm, "Crear y editar clientes"),
        new("clients:delete", ModuleCrm, "Eliminar clientes"),

        // ── Cotizaciones ─────────────────────────────────────────────────────────────────────
        new("quotes:read", ModuleQuotes, "Ver cotizaciones"),
        new("quotes:create", ModuleQuotes, "Emitir cotizaciones"),
        new("quotes:update", ModuleQuotes, "Editar cotizaciones"),
        new("quotes:accept", ModuleQuotes, "Aceptar una cotización (compromete el vehículo)"),
        new("quotes:reject", ModuleQuotes, "Rechazar cotizaciones"),
        new("quotes:delete", ModuleQuotes, "Eliminar cotizaciones"),

        // ── Ventas ───────────────────────────────────────────────────────────────────────────
        new("sales:read", ModuleSales, "Ver las ventas"),
        new("sales:create", ModuleSales, "Registrar ventas"),
        new("sales:update", ModuleSales, "Editar ventas"),
        new("sales:delete", ModuleSales, "Eliminar ventas"),
        new("sales:cancel", ModuleSales, "Cancelar ventas"),

        // ── Finanzas ─────────────────────────────────────────────────────────────────────────
        new("financial:read", ModuleFinance, "Ver los movimientos financieros"),
        new("financial:create", ModuleFinance, "Registrar movimientos"),
        new("financial:update", ModuleFinance, "Editar movimientos"),
        new("financial:delete", ModuleFinance, "Eliminar movimientos"),

        // ── Agenda ───────────────────────────────────────────────────────────────────────────
        new("appointments:read", ModuleAgenda, "Ver la agenda"),
        new("appointments:create", ModuleAgenda, "Agendar turnos"),
        new("appointments:update", ModuleAgenda, "Editar turnos"),
        new("appointments:delete", ModuleAgenda, "Eliminar turnos"),

        // ── Documentación ────────────────────────────────────────────────────────────────────
        new("documents:read", ModuleDocuments, "Ver documentos adjuntos"),
        new("documents:create", ModuleDocuments, "Adjuntar documentos"),

        // ── Administración ───────────────────────────────────────────────────────────────────
        new("users:access", ModuleAdministration, "Ver el equipo"),
        new("users:create", ModuleAdministration, "Dar de alta usuarios"),
        new("CanManageUsers", ModuleAdministration, "Administrar usuarios"),
        new("CanManageRoles", ModuleAdministration, "Administrar roles y permisos"),
        new("CanManageSettings", ModuleAdministration, "Configurar la concesionaria"),
        new("webhooks:manage", ModuleAdministration, "Administrar webhooks"),
        new("admin:backfill", ModuleAdministration, "Ejecutar tareas de mantenimiento de datos"),
    ];

    /// <summary>Every grantable permission, in the order the matrix should render them.</summary>
    public static IReadOnlyList<PermissionDefinition> All => Definitions;

    private static readonly HashSet<string> Values =
        Definitions.Select(d => d.Value).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Whether a dealership may grant this permission. Case-sensitive on purpose: the endpoints
    /// compare the claim exactly, so <c>Cars:Read</c> would be stored, displayed as granted, and
    /// silently never match.
    /// </summary>
    public static bool IsGrantable(string permission) => Values.Contains(permission);

    /// <summary>
    /// The permissions a newly provisioned dealership's Admin role receives: all of them. The
    /// first user of a dealership has to be able to reach everything, including the screen that
    /// creates the narrower roles.
    /// </summary>
    public static IReadOnlyList<string> AdminPermissions => Definitions.Select(d => d.Value).ToArray();
}
