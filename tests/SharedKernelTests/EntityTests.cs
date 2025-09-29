using SharedKernel;

public class EntityTests
{
    private sealed class TestEntity : Entity { }
    private sealed record TestDomainEvent() : IDomainEvent;

    [Fact]
    public void IdProperty_CanBeSetAndRetrieved()
    {
        // Arrange
        var entity = new TestEntity();
        var id = Guid.NewGuid();

        // Act
        entity.Id = id;

        // Assert
        entity.Id.Should().Be(id);
    }

    [Fact]
    public void Raise_AddsDomainEvent()
    {
        // Arrange
        var entity = new TestEntity();
        var domainEvent = new TestDomainEvent();

        // Act
        entity.Raise(domainEvent);

        // Assert
        entity.DomainEvents.Should().ContainSingle().Which.Should().Be(domainEvent);
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        // Arrange
        var entity = new TestEntity();
        entity.Raise(new TestDomainEvent());

        // Act
        entity.ClearDomainEvents();

        // Assert
        entity.DomainEvents.Should().BeEmpty();
    }
}
