using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Application.Appointments.Queries.GetAppointments;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Infrastructure.Database;
using Xunit;

namespace WebApiTests.Postgres;

/// <summary>
/// Guards against EF model drift on <c>Appointment.Status</c>.
///
/// <para>
/// <c>AppointmentConfiguration</c> maps <c>Status</c> and
/// <c>GetAppointmentsQueryHandler</c> projects it, so the column must exist in a
/// migrated database. A build alone cannot catch a missing migration: the mapping
/// compiles fine and only blows up at query time with Postgres 42703
/// (<c>column a.status does not exist</c>).
/// </para>
///
/// <para>
/// These tests run against a migrated Testcontainers Postgres
/// (<see cref="PostgresWebApplicationFactory.InitializeDatabaseAsync"/> calls
/// <c>MigrateAsync</c>), so they fail whenever the mapping outruns the migrations.
/// </para>
/// </summary>
[Collection("PostgresCollection")]
[Trait("Category", "Postgres")]
public class AppointmentStatusColumnPostgresTests : IAsyncLifetime
{
    private readonly PostgresWebApplicationFactory _factory;

    public AppointmentStatusColumnPostgresTests(PostgresFixture fixture)
    {
        _factory = new PostgresWebApplicationFactory(fixture.GetConnectionString());
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var client = _factory.CreateClient();
        var loginRequest = new
        {
            Email = "admin@carstore.com",
            Password = "Admin123!"
        };

        var loginResponse = await client.PostAsJsonAsync("/api/v1/users/login", loginRequest, IntegrationTestHelpers.JsonOptions);
        loginResponse.EnsureSuccessStatusCode();

        var result = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(IntegrationTestHelpers.JsonOptions);
        return result!.Token;
    }

    private sealed record LoginResponse(string Token);

    /// <summary>
    /// Catches the drift class that produced the appointments 500: a mapping was added to
    /// <c>AppointmentConfiguration</c> and back-patched into the already-applied
    /// <c>20260527221933_AddAppointments</c> migration instead of being shipped as a new one.
    /// Fresh databases replay the edited migration and look healthy, so every other test here
    /// passes while databases that already recorded that migration never receive the column.
    /// The model snapshot is the one artifact that records the discrepancy.
    /// </summary>
    [Fact]
    public void Model_ShouldHaveNoPendingChangesAgainstMigrations()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.Database.HasPendingModelChanges()
            .Should().BeFalse("every mapping change must ship as a new migration — back-patching an applied migration leaves existing databases without the column");
    }

    [Fact]
    public async Task Migrations_ShouldCreateStatusColumnOnAppointments()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT data_type, is_nullable, column_default
            FROM information_schema.columns
            WHERE table_name = 'appointments' AND column_name = 'status'
            """;

        using var reader = await command.ExecuteReaderAsync();

        (await reader.ReadAsync())
            .Should().BeTrue("the appointments table must have a status column — AppointmentConfiguration maps Appointment.Status");

        reader.GetString(0).Should().Be("character varying");
        reader.GetString(1).Should().Be("NO", "Status is mapped as IsRequired()");
        reader.GetValue(2).ToString().Should().Contain("Scheduled", "Status is mapped with HasDefaultValue(AppointmentStatus.Scheduled)");
    }

    [Fact]
    public async Task GetAppointments_WithValidRange_ShouldReturnOk()
    {
        var token = await GetAdminTokenAsync();
        var client = _factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var from = DateTime.UtcNow.AddDays(-30).ToString("O");
        var to = DateTime.UtcNow.AddDays(30).ToString("O");

        var response = await client.GetAsync($"/api/v1/appointments?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}");

        if (response.StatusCode != HttpStatusCode.OK)
        {
            var err = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, because: $"Response error: {err}");
        }

        var appointments = await response.Content.ReadFromJsonAsync<List<AppointmentDto>>(IntegrationTestHelpers.JsonOptions);
        appointments.Should().NotBeNull();
    }
}
