using Domain.Leads;

namespace DomainTests;

public class LeadActivityTests
{
    private static readonly Guid DealerId = Guid.NewGuid();
    private static readonly Guid LeadId = Guid.NewGuid();

    [Fact]
    public void Record_ShouldTrimTheDescription()
    {
        var activity = LeadActivity.Record(
            DealerId, LeadId, LeadActivityType.StatusChanged, "  Pasó a Contactado  ", DateTime.UtcNow);

        activity.Description.Should().Be("Pasó a Contactado");
        activity.Type.Should().Be(LeadActivityType.StatusChanged);
        activity.LeadId.Should().Be(LeadId);
    }

    [Fact]
    public void Record_ShouldThrow_WhenLeadIdIsEmpty()
    {
        Action act = () => LeadActivity.Record(
            DealerId, Guid.Empty, LeadActivityType.Created, "algo", DateTime.UtcNow);

        act.Should().Throw<DomainException>().WithMessage("*LeadId*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Record_ShouldThrow_WhenDescriptionIsBlank(string description)
    {
        Action act = () => LeadActivity.Record(
            DealerId, LeadId, LeadActivityType.Created, description, DateTime.UtcNow);

        act.Should().Throw<DomainException>().WithMessage("*description*");
    }

    /// <summary>
    /// A related id with no type is a dead link — the UI knows an id but not where to send the
    /// reader, so the entry renders as unclickable text and the reference is effectively lost.
    /// </summary>
    [Fact]
    public void Record_ShouldThrow_WhenRelatedIdHasNoType()
    {
        Action act = () => LeadActivity.Record(
            DealerId, LeadId, LeadActivityType.QuoteCreated, "Cotización creada", DateTime.UtcNow,
            relatedEntityId: Guid.NewGuid());

        act.Should().Throw<DomainException>().WithMessage("*RelatedEntityType*");
    }

    [Fact]
    public void Record_ShouldKeepTheRelatedReference_WhenBothPartsAreGiven()
    {
        var quoteId = Guid.NewGuid();

        var activity = LeadActivity.Record(
            DealerId, LeadId, LeadActivityType.QuoteCreated, "Cotización creada", DateTime.UtcNow,
            relatedEntityId: quoteId, relatedEntityType: "Quote");

        activity.RelatedEntityId.Should().Be(quoteId);
        activity.RelatedEntityType.Should().Be("Quote");
    }

    /// <summary>Outbox-driven entries have no acting user, and that is not an error.</summary>
    [Fact]
    public void Record_ShouldAllowANullActor()
    {
        var activity = LeadActivity.Record(
            DealerId, LeadId, LeadActivityType.QuoteAccepted, "Cotización aceptada", DateTime.UtcNow);

        activity.ActorUserId.Should().BeNull();
    }

    [Fact]
    public void Record_ShouldDropAStrayTypeWhenThereIsNoRelatedId()
    {
        var activity = LeadActivity.Record(
            DealerId, LeadId, LeadActivityType.NoteAdded, "Nota", DateTime.UtcNow,
            relatedEntityId: null, relatedEntityType: "Quote");

        activity.RelatedEntityType.Should().BeNull("a type without an id points at nothing");
    }
}
