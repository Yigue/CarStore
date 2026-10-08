using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Caching;
using Application.Marcas.Create;
using Domain.Cars.Attributes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Application.UnitTests.Catalog;

public class MarcaCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenMarcaExistsWithDifferentCasing()
    {
        using var context = CreateContext();
        var existingMarca = new Marca("Toyota");
        context.Marca.Add(existingMarca);
        await context.SaveChangesAsync();

        var mockBrandService = new Mock<ICachedBrandService>();
        var handler = new CreateMarcaCommandHandler(context, mockBrandService.Object);

        // Attempt to create "TOYOTA" when "Toyota" already exists
        var command = new CreateMarcaCommand("TOYOTA");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Marca.AlreadyExists");
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenMarcaExistsWithWhitespaceAndDifferentCasing()
    {
        using var context = CreateContext();
        var existingMarca = new Marca("Ford");
        context.Marca.Add(existingMarca);
        await context.SaveChangesAsync();

        var mockBrandService = new Mock<ICachedBrandService>();
        var handler = new CreateMarcaCommandHandler(context, mockBrandService.Object);

        var command = new CreateMarcaCommand("   ford   ");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Marca.AlreadyExists");
    }

    [Fact]
    public async Task Handle_Should_CreateMarca_WhenNameIsUnique()
    {
        using var context = CreateContext();
        var mockBrandService = new Mock<ICachedBrandService>();
        var handler = new CreateMarcaCommandHandler(context, mockBrandService.Object);

        var command = new CreateMarcaCommand("Honda");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Marca.Should().ContainSingle(m => m.Id == result.Value);
    }
}
