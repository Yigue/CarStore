using Domain.Clients;
using Domain.Clients.Attributes;
using Domain.Clients.Events;

namespace DomainTests.Clients;

/// <summary>
/// PR1 RED→GREEN: Delete / Restore / UpdateNotes domain-method invariants.
/// </summary>
public class ClientAggregateTests
{
    private static Client BuildClient(string? email = null, string? dni = null)
    {
        var faker = new Faker();
        return new Client(
            Guid.NewGuid(),
            faker.Name.FirstName(),
            faker.Name.LastName(),
            dni ?? faker.Random.ReplaceNumbers("########"),
            email ?? faker.Internet.Email(),
            faker.Phone.PhoneNumber(),
            faker.Address.FullAddress(),
            DateTime.UtcNow);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public void Delete_Should_Set_IsDeleted_True_And_Raise_ClientSoftDeletedDomainEvent()
    {
        var client = BuildClient();
        client.ClearDomainEvents();
        var actorId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        client.Delete(actorId, now);

        client.IsDeleted.Should().BeTrue();
        client.DeletedAtUtc.Should().Be(now);
        client.DeletedBy.Should().Be(actorId);

        client.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ClientSoftDeletedDomainEvent>()
            .Subject.Should().Match<ClientSoftDeletedDomainEvent>(e =>
                e.ClientId == client.Id
                && e.DeletedAtUtc == now
                && e.DeletedBy == actorId);
    }

    [Fact]
    public void Delete_Should_Be_Idempotent_When_Already_Deleted()
    {
        var client = BuildClient();
        var actorId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        client.Delete(actorId, now);
        client.ClearDomainEvents();

        // Second call — should succeed without raising a new event
        client.Delete(actorId, now.AddSeconds(1));

        client.IsDeleted.Should().BeTrue();
        client.DomainEvents.Should().BeEmpty("already-deleted is a no-op");
    }

    // ── Restore ───────────────────────────────────────────────────────────────

    [Fact]
    public void Restore_Should_Clear_IsDeleted_And_Raise_ClientRestoredDomainEvent()
    {
        var client = BuildClient();
        var actorId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        client.Delete(actorId, now);
        client.ClearDomainEvents();

        client.Restore(actorId, now.AddMinutes(5));

        client.IsDeleted.Should().BeFalse();
        client.DeletedAtUtc.Should().BeNull();

        client.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ClientRestoredDomainEvent>()
            .Subject.Should().Match<ClientRestoredDomainEvent>(e =>
                e.ClientId == client.Id
                && e.RestoredBy == actorId);
    }

    [Fact]
    public void Restore_Should_Return_Failure_When_Client_Is_Not_Deleted()
    {
        var client = BuildClient();
        client.ClearDomainEvents();

        var result = client.Restore(Guid.NewGuid(), DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClientErrors.NotDeleted(client.Id));
        client.DomainEvents.Should().BeEmpty();
    }

    // ── UpdateNotes ───────────────────────────────────────────────────────────

    [Fact]
    public void UpdateNotes_Should_Set_Notes_And_Raise_ClientNotesUpdatedDomainEvent()
    {
        var client = BuildClient();
        client.ClearDomainEvents();
        var actorId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var result = client.UpdateNotes("VIP — prefers SUV", actorId, now);

        result.IsSuccess.Should().BeTrue();
        client.Notes.Should().Be("VIP — prefers SUV");

        client.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ClientNotesUpdatedDomainEvent>()
            .Subject.Should().Match<ClientNotesUpdatedDomainEvent>(e =>
                e.ClientId == client.Id
                && e.ActorId == actorId
                && e.OccurredAt == now);
    }

    [Fact]
    public void UpdateNotes_Should_Fail_When_Notes_Exceed_2000_Chars()
    {
        var client = BuildClient();
        client.ClearDomainEvents();
        var tooLong = new string('x', 2001);

        var result = client.UpdateNotes(tooLong, Guid.NewGuid(), DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClientErrors.NotesTooLong());
        client.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateNotes_Should_Allow_Null_To_Clear_Notes()
    {
        var client = BuildClient();
        client.ClearDomainEvents();

        var result = client.UpdateNotes(null, Guid.NewGuid(), DateTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        client.Notes.Should().BeNull();
    }

    // ── AcquisitionSource ─────────────────────────────────────────────────────

    [Fact]
    public void AcquisitionSource_Enum_Should_Have_Four_Values()
    {
        var values = Enum.GetValues<AcquisitionSource>();
        values.Should().HaveCount(4);
        values.Should().Contain(AcquisitionSource.Web);
        values.Should().Contain(AcquisitionSource.Portal);
        values.Should().Contain(AcquisitionSource.Referral);
        values.Should().Contain(AcquisitionSource.Otro);
    }
}
