using System.Data;
using System.Data.Common;
using Application.Abstractions.Tenancy;
using Application.Appointments.Queries.GetAppointments;
using Domain.Appointments;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.DealerSettings;
using Domain.Users;
using Domain.Shared.ValueObjects;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Database;

namespace InfrastructureEfTests.Appointments;

/// <summary>
/// Integration tests for GetAppointmentsQueryHandler using a real SQLite relational
/// ApplicationDbContext. These tests reproduce and guard against BUG#1: the null-conditional
/// JOIN key on Marca/Modelo caused incorrect query results (rows with orphaned cars excluded)
/// and on Npgsql causes an InvalidOperationException during LINQ→SQL translation.
/// EF InMemory would give a false green; SQLite relational exercises the actual SQL path.
/// </summary>
public class GetAppointmentsQueryHandlerTests
{
    private static readonly Guid TestDealerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private sealed class FakeCurrentTenantService : ICurrentTenantService
    {
        public Guid DealerId => TestDealerId;
        public bool HasTenant => true;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private static async Task<(ApplicationDbContext Context, SqliteConnection Connection)> CreateContextAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys = ON;";
            await cmd.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .UseSnakeCaseNamingConvention()
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var tenantService = new FakeCurrentTenantService();
        var context = new ApplicationDbContext(options, new NoOpPublisher(), tenantService);
        await context.Database.EnsureCreatedAsync();

        // Seed DealerSettings — required FK for most entities
        var settings = new DealerSettings(TestDealerId, "Test Dealer", "test@dealer.com");
        context.DealerSettings.Add(settings);
        await context.SaveChangesAsync();

        return (context, connection);
    }

    /// <summary>
    /// RED test for BUG#1.
    /// Pre-fix: the null-conditional in JOIN ON (`car != null ? car.MarcaId : Guid.Empty`)
    /// acts as an implicit INNER JOIN on the Marca/Modelo tables for the buggy query path,
    /// causing appointments with orphaned VehicleId (no matching Car row) to be silently
    /// dropped from results. On Npgsql this throws InvalidOperationException at translation.
    /// On SQLite it manifests as missing rows.
    /// Post-fix: both appointments are returned — the valid car row has VehicleName,
    /// the orphaned row has VehicleName=null (null-propagated LEFT JOIN).
    /// </summary>
    [Fact]
    public async Task Handle_Should_ReturnRows_WhenAppointmentsHaveNullAndValidVehicle()
    {
        var (context, connection) = await CreateContextAsync();
        await using var _ = context;
        await using var __ = connection;

        // Arrange — seed catalog entities
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        await context.SaveChangesAsync();

        // A real Car linked to Marca/Modelo
        var car = new Car(
            TestDealerId, marca, modelo,
            Color.Black, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 1600, 1000, 2022, "TOY001", "desc", 20000m, DateTime.UtcNow);
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        // A User to act as agent (AgentId required by Appointment.Create)
        var agent = new User(TestDealerId, "agent@test.com", "Agent", "One", "hash", Guid.NewGuid());
        context.Users.Add(agent);
        await context.SaveChangesAsync();

        // A Client for the appointments
        var client = new Client(TestDealerId, "John", "Doe", "DNI001", "john@test.com", "555", "Addr", DateTime.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        var from = DateTime.UtcNow.AddHours(-1);
        var to = DateTime.UtcNow.AddHours(3);

        // Appointment #1 — valid Car, Client
        var appt1 = Appointment.Create(
            TestDealerId,
            car.Id,
            client.Id,
            null,
            agent.Id,
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1),
            AppointmentType.TestDrive,
            "valid car",
            DateTime.UtcNow);
        appt1.ClearDomainEvents();
        context.Appointments.Add(appt1);
        await context.SaveChangesAsync();

        // Appointment #2 — orphaned VehicleId (no matching Car row).
        // We bypass FK enforcement to simulate a walk-in or a car that was deleted.
        // Disable FK checks on this connection, insert raw, re-enable.
        var orphanedCarId = Guid.NewGuid();
        var appt2Id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Determine actual column names used by EF Core for SQLite (no snake_case convention in tests)
        // by querying the schema before inserting.
        string appointmentsTable = "Appointments";
        List<string> columnNames;
        using (var schemaCmd = connection.CreateCommand())
        {
            schemaCmd.CommandText = $"PRAGMA table_info(\"{appointmentsTable}\");";
            using var reader = await schemaCmd.ExecuteReaderAsync();
            columnNames = [];
            while (await reader.ReadAsync())
            {
                columnNames.Add(reader.GetString(1)); // column 1 = name
            }
        }

        // If EF used snake_case, fall back; otherwise use PascalCase
        bool useSnakeCase = columnNames.Contains("dealer_id");
        string colId = useSnakeCase ? "id" : "Id";
        string colDealer = useSnakeCase ? "dealer_id" : "DealerId";
        string colVehicle = useSnakeCase ? "vehicle_id" : "VehicleId";
        string colClient = useSnakeCase ? "client_id" : "ClientId";
        string colAgent = useSnakeCase ? "agent_id" : "AgentId";
        string colLead = useSnakeCase ? "lead_id" : "LeadId";
        string colStart = useSnakeCase ? "start_date_time" : "StartDateTime";
        string colEnd = useSnakeCase ? "end_date_time" : "EndDateTime";
        string colType = useSnakeCase ? "type" : "Type";
        string colNotes = useSnakeCase ? "notes" : "Notes";
        string colCreated = useSnakeCase ? "created_at" : "CreatedAt";

        using (var fkOff = connection.CreateCommand())
        {
            fkOff.CommandText = "PRAGMA foreign_keys = OFF;";
            await fkOff.ExecuteNonQueryAsync();
        }

        using (var insertCmd = connection.CreateCommand())
        {
            insertCmd.CommandText = $@"
                INSERT INTO ""{appointmentsTable}""
                    (""{colId}"", ""{colDealer}"", ""{colVehicle}"", ""{colClient}"", ""{colAgent}"", ""{colLead}"",
                     ""{colStart}"", ""{colEnd}"", ""{colType}"", ""{colNotes}"", ""{colCreated}"")
                VALUES
                    ($id, $dealer, $vehicle, $client, $agent, NULL,
                     $start, $end, 'TestDrive', 'orphan car', $created)";
            // EF Core SQLite stores DateTime as "yyyy-MM-dd HH:mm:ss.fffffff" (no T, no Z)
            const string sqliteDateFmt = "yyyy-MM-dd HH:mm:ss.fffffff";
            insertCmd.Parameters.AddWithValue("$id", appt2Id.ToString());
            insertCmd.Parameters.AddWithValue("$dealer", TestDealerId.ToString());
            insertCmd.Parameters.AddWithValue("$vehicle", orphanedCarId.ToString());
            insertCmd.Parameters.AddWithValue("$client", client.Id.ToString());
            insertCmd.Parameters.AddWithValue("$agent", agent.Id.ToString());
            insertCmd.Parameters.AddWithValue("$start", now.ToString(sqliteDateFmt));
            insertCmd.Parameters.AddWithValue("$end", now.AddHours(1).ToString(sqliteDateFmt));
            insertCmd.Parameters.AddWithValue("$created", now.ToString(sqliteDateFmt));
            var rowsAffected = await insertCmd.ExecuteNonQueryAsync();
            rowsAffected.Should().Be(1, "the orphaned appointment raw INSERT should succeed with FK OFF");
        }

        using (var fkOn = connection.CreateCommand())
        {
            fkOn.CommandText = "PRAGMA foreign_keys = ON;";
            await fkOn.ExecuteNonQueryAsync();
        }

        // Verify the appointment was inserted
        using (var countCmd = connection.CreateCommand())
        {
            countCmd.CommandText = $"SELECT COUNT(*) FROM \"{appointmentsTable}\"";
            var count = (long)(await countCmd.ExecuteScalarAsync())!;
            count.Should().Be(2, "both appointments should be in the table before the handler runs");
        }

        var tenantService = new FakeCurrentTenantService();
        var handler = new GetAppointmentsQueryHandler(context, tenantService);

        // Act
        var result = await handler.Handle(new GetAppointmentsQuery(from, to), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2, "both appointments (valid car and orphaned car) should be returned");

        var validCarRow = result.Value.First(r => r.VehicleId == car.Id);
        validCarRow.VehicleName.Should().NotBeNullOrWhiteSpace();
        validCarRow.VehicleName.Should().Contain("Toyota").And.Contain("Corolla");

        var orphanRow = result.Value.First(r => r.VehicleId == orphanedCarId);
        orphanRow.VehicleName.Should().BeNull(
            "when the car row doesn't exist, the LEFT JOIN returns null and VehicleName should be null");
    }

    /// <summary>
    /// A lead-linked appointment must carry its LeadId to the client.
    ///
    /// CreateAppointmentCommandValidator enforces `ClientId.HasValue ^ LeadId.HasValue`, so an
    /// appointment against a Lead is not an edge case — it is half of every valid appointment.
    /// The projection carried ClientId but not LeadId, so those rows arrived with both ids null.
    /// The name still rendered (ClientName falls back to the lead's), which is exactly why the
    /// gap was invisible: the row looked right and simply had nowhere to navigate to. The
    /// frontend's own AppointmentDto already declared `leadId`, so the field was typed and
    /// permanently undefined.
    /// </summary>
    [Fact]
    public async Task Handle_Should_ProjectLeadId_ForLeadLinkedAppointments()
    {
        var (context, connection) = await CreateContextAsync();
        await using var _ = context;
        await using var __ = connection;

        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        await context.SaveChangesAsync();

        var car = new Car(
            TestDealerId, marca, modelo,
            Color.Black, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 1600, 1000, 2022, "LDA001", "desc", 20000m, DateTime.UtcNow);
        context.Cars.Add(car);

        var agent = new User(TestDealerId, "agent2@test.com", "Agent", "Two", "hash", Guid.NewGuid());
        context.Users.Add(agent);

        var client = new Client(TestDealerId, "Jane", "Roe", "DNI002", "jane@test.com", "555", "Addr", DateTime.UtcNow);
        context.Clients.Add(client);

        var lead = Domain.Leads.Lead.Create(
            TestDealerId, "Carlos Lead", "carlos@test.com", "555-0100",
            Domain.Leads.LeadSource.Web, DateTime.UtcNow);
        lead.ClearDomainEvents();
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        var from = DateTime.UtcNow.AddHours(-1);
        var to = DateTime.UtcNow.AddHours(3);

        // Exactly one of ClientId / LeadId, per the create validator.
        var leadAppt = Appointment.Create(
            TestDealerId, car.Id, null, lead.Id, agent.Id,
            DateTime.UtcNow, DateTime.UtcNow.AddMinutes(30),
            AppointmentType.TestDrive, "lead appointment", DateTime.UtcNow);
        leadAppt.ClearDomainEvents();

        var clientAppt = Appointment.Create(
            TestDealerId, car.Id, client.Id, null, agent.Id,
            DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(1).AddMinutes(30),
            AppointmentType.TestDrive, "client appointment", DateTime.UtcNow);
        clientAppt.ClearDomainEvents();

        context.Appointments.AddRange(leadAppt, clientAppt);
        await context.SaveChangesAsync();

        var handler = new GetAppointmentsQueryHandler(context, new FakeCurrentTenantService());
        var result = await handler.Handle(new GetAppointmentsQuery(from, to), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var leadRow = result.Value.Single(r => r.Id == leadAppt.Id);
        leadRow.LeadId.Should().Be(lead.Id, "a lead-linked appointment must expose the lead it came from");
        leadRow.ClientId.Should().BeNull();
        leadRow.ClientName.Should().Be("Carlos Lead", "the name falls back to the lead's — this part already worked");

        var clientRow = result.Value.Single(r => r.Id == clientAppt.Id);
        clientRow.LeadId.Should().BeNull("a client-linked appointment has no lead");
        clientRow.ClientId.Should().Be(client.Id);
    }
}
