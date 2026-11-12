using Application.Abstractions.Caching;
using Application.Modelos.Create;
using Domain.Cars.Attributes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Catalog;

/// <summary>
/// sfc B7.5 / RN-040: a model name is unique within its brand regardless of case, like brand names already are. The
/// same name under another brand stays allowed.
/// </summary>
public class ModeloUniquenessTests
{
    [Fact]
    public async Task Create_Should_RejectTheSameModelName_InADifferentCase_ForTheSameBrand_ButAllowItForAnother()
    {
        using var context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var toyota = new Marca("Toyota");
        var other = new Marca("Lexus");
        context.Marca.AddRange(toyota, other);
        context.Modelo.Add(new Modelo("Corolla", toyota.Id));
        await context.SaveChangesAsync();
        var handler = new CreateModeloCommandHandler(context, new Mock<ICachedModelService>().Object);

        var duplicate = await handler.Handle(new CreateModeloCommand("  COROLLA ", toyota.Id), CancellationToken.None);
        var otherBrand = await handler.Handle(new CreateModeloCommand("corolla", other.Id), CancellationToken.None);

        duplicate.IsFailure.Should().BeTrue();
        duplicate.Error.Code.Should().Be("Modelo.AlreadyExists");
        otherBrand.IsSuccess.Should().BeTrue();
    }
}
