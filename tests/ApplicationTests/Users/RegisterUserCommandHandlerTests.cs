using Application.Abstractions.Authentication;
using Application.Abstractions.Tenancy;
using Application.Users.Register;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Moq;

using Application.UnitTests;

namespace ApplicationTests.Users;

public class RegisterUserCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_ReturnsTenantRequiredFailure_WhenTenantServiceHasEmptyDealerId()
    {
        // Arrange
        using var context = CreateContext();
        var passwordHasher = new Mock<IPasswordHasher>();
        passwordHasher.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed_password");

        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Empty);
        tenantService.Setup(t => t.HasTenant).Returns(false);

        var handler = new RegisterUserCommandHandler(context, passwordHasher.Object, tenantService.Object);
        var command = new RegisterUserCommand("newuser@example.com", "John", "Doe", "Pass123!");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.TenantRequired);
        context.Users.Any().Should().BeFalse("No user should be created without a valid tenant");
    }

    [Fact]
    public async Task Handle_AssignsClienteRole_WhenTenantIsPresent()
    {
        // Arrange
        using var context = CreateContext();
        var dealerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var passwordHasher = new Mock<IPasswordHasher>();
        passwordHasher.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed_password");

        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(dealerId);
        tenantService.Setup(t => t.HasTenant).Returns(true);

        var handler = new RegisterUserCommandHandler(context, passwordHasher.Object, tenantService.Object);
        var command = new RegisterUserCommand("newuser@example.com", "Jane", "Doe", "Pass123!");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var createdUser = await context.Users.FirstAsync(u => u.Id == result.Value);
        createdUser.DealerId.Should().Be(dealerId);
        createdUser.RoleId.Should().NotBe(Guid.Empty, "Registered user must have a non-empty RoleId");

        var assignedRole = await context.Roles.FirstOrDefaultAsync(r => r.Id == createdUser.RoleId);
        assignedRole.Should().NotBeNull();
        assignedRole!.Name.Should().Be("Cliente");
        assignedRole.DealerId.Should().Be(dealerId);
    }

    [Fact]
    public async Task Handle_ReusesExistingClienteRole_WhenAlreadyPresent()
    {
        // Arrange
        using var context = CreateContext();
        var dealerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var existingClienteRole = new Role(dealerId, "Cliente", "Rol de clientes existente");
        context.Roles.Add(existingClienteRole);
        await context.SaveChangesAsync();

        var passwordHasher = new Mock<IPasswordHasher>();
        passwordHasher.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed_password");

        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(dealerId);
        tenantService.Setup(t => t.HasTenant).Returns(true);

        var handler = new RegisterUserCommandHandler(context, passwordHasher.Object, tenantService.Object);
        var command = new RegisterUserCommand("seconduser@example.com", "Bob", "Smith", "Pass123!");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var createdUser = await context.Users.FirstAsync(u => u.Id == result.Value);
        createdUser.RoleId.Should().Be(existingClienteRole.Id);

        var rolesCount = await context.Roles.CountAsync(r => r.DealerId == dealerId && r.Name == "Cliente");
        rolesCount.Should().Be(1, "Should not duplicate the Cliente role if it already exists");
    }
}
