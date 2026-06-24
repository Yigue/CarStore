using Domain.Leads;
using Domain.Leads.Events;

namespace DomainTests;

public class LeadTests
{
    private readonly Faker _faker = new();

    private Lead CreateNewLead()
    {
        return Lead.Create(
            Guid.NewGuid(),
            _faker.Name.FullName(),
            _faker.Internet.Email(),
            _faker.Phone.PhoneNumber(),
            LeadSource.Web,
            DateTime.UtcNow);
    }

    // ─── Creation ──────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ShouldSetDefaultStatus_Nuevo()
    {
        var lead = CreateNewLead();
        lead.Status.Should().Be(LeadStatus.Nuevo);
    }

    [Fact]
    public void Create_ShouldSetInterestedVehicleId_WhenProvided()
    {
        var vehicleId = Guid.NewGuid();
        var lead = Lead.Create(
            Guid.NewGuid(),
            _faker.Name.FullName(),
            _faker.Internet.Email(),
            _faker.Phone.PhoneNumber(),
            LeadSource.Web,
            DateTime.UtcNow,
            vehicleId);

        lead.InterestedVehicleId.Should().Be(vehicleId);
    }

    [Fact]
    public void Create_ShouldLeaveInterestedVehicleId_Null_WhenNotProvided()
    {
        var lead = CreateNewLead();
        lead.InterestedVehicleId.Should().BeNull();
    }

    // ─── LinkVehicle ───────────────────────────────────────────────────────────

    [Fact]
    public void LinkVehicle_ShouldSetInterestedVehicleId()
    {
        var lead = CreateNewLead();
        var vehicleId = Guid.NewGuid();

        lead.LinkVehicle(vehicleId);

        lead.InterestedVehicleId.Should().Be(vehicleId);
    }

    [Fact]
    public void LinkVehicle_ShouldThrow_WhenVehicleIdEmpty()
    {
        var lead = CreateNewLead();

        var act = () => lead.LinkVehicle(Guid.Empty);

        act.Should().Throw<DomainException>()
            .WithMessage("*vehicle*");
    }

    // ─── Status transitions ────────────────────────────────────────────────────

    [Fact]
    public void UpdateStatus_ShouldAllowSequentialForwardTransition()
    {
        var lead = CreateNewLead();

        var act = () => lead.UpdateStatus(LeadStatus.Contactado, "First contact made");

        act.Should().NotThrow();
        lead.Status.Should().Be(LeadStatus.Contactado);
    }

    [Fact]
    public void UpdateStatus_ShouldThrow_WhenSkippingStages()
    {
        var lead = CreateNewLead();

        var act = () => lead.UpdateStatus(LeadStatus.Negociacion, null);

        act.Should().Throw<DomainException>()
            .WithMessage("*sequential*");
    }

    [Fact]
    public void UpdateStatus_ToContactado_ShouldRequireNotes()
    {
        var lead = CreateNewLead();

        var act = () => lead.UpdateStatus(LeadStatus.Contactado, null);

        act.Should().Throw<DomainException>()
            .WithMessage("*notes*");
    }

    [Fact]
    public void UpdateStatus_ToDemostracion_ShouldRequireVehicle()
    {
        var lead = CreateNewLead();
        lead.UpdateStatus(LeadStatus.Contactado, "first contact");

        var act = () => lead.UpdateStatus(LeadStatus.Demostracion, null);

        act.Should().Throw<DomainException>()
            .WithMessage("*vehicle*");
    }

    [Fact]
    public void UpdateStatus_ToDemostracion_ShouldSucceed_WhenVehicleLinked()
    {
        var lead = CreateNewLead();
        lead.LinkVehicle(Guid.NewGuid());
        lead.UpdateStatus(LeadStatus.Contactado, "contacted");

        var act = () => lead.UpdateStatus(LeadStatus.Demostracion, null);

        act.Should().NotThrow();
    }

    [Fact]
    public void UpdateStatus_ToPerdido_ShouldRequireLossReason()
    {
        var lead = CreateNewLead();
        lead.UpdateStatus(LeadStatus.Contactado, "contacted");

        var act = () => lead.UpdateStatus(LeadStatus.Perdido, null, null);

        act.Should().Throw<DomainException>()
            .WithMessage("*loss reason*");
    }

    [Fact]
    public void UpdateStatus_ToPerdido_ShouldSucceed_WhenLossReasonProvided()
    {
        var lead = CreateNewLead();
        lead.UpdateStatus(LeadStatus.Contactado, "contacted");

        var act = () => lead.UpdateStatus(LeadStatus.Perdido, null, LeadLossReason.Precio);

        act.Should().NotThrow();
        lead.LossReason.Should().Be(LeadLossReason.Precio);
    }

    [Fact]
    public void UpdateStatus_ShouldThrow_WhenMovingBackward_FromGanado()
    {
        var lead = CreateNewLead();
        lead.LinkVehicle(Guid.NewGuid());
        lead.UpdateStatus(LeadStatus.Contactado, "contacted");
        lead.UpdateStatus(LeadStatus.Demostracion, null);
        lead.UpdateStatus(LeadStatus.Negociacion, null);
        lead.UpdateStatus(LeadStatus.Ganado, null);

        var act = () => lead.UpdateStatus(LeadStatus.Negociacion, null);

        act.Should().Throw<DomainException>();
    }

    // ─── Archive ───────────────────────────────────────────────────────────────

    [Fact]
    public void Archive_ShouldSetStatusToArchivado()
    {
        var lead = CreateNewLead();

        lead.Archive();

        lead.Status.Should().Be(LeadStatus.Archivado);
    }

    // ─── ConvertToClient ───────────────────────────────────────────────────────

    [Fact]
    public void MarkConverted_ShouldSetConvertedClientId()
    {
        var lead = CreateNewLead();
        var clientId = Guid.NewGuid();

        lead.MarkConverted(clientId);

        lead.ConvertedClientId.Should().Be(clientId);
    }
}
