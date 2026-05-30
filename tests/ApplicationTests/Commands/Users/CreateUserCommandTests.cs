using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Tenancy;
using Application.Users.Commands.CreateUser;
using Domain.Shared.ValueObjects;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Application.UnitTests.Commands.Users;

public class CreateUserCommandTests
{
    private static TestApplicationDbContext CreateContext(Guid? dealerId = null)
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options, dealerId ?? Guid.NewGuid());
    }

    private static CreateUserCommandHandler CreateHandler(
        TestApplicationDbContext context,
        IPasswordHasher? passwordHasher = null,
        ICurrentTenantService? tenantService = null)
    {
        var mockPasswordHasher = new Mock<IPasswordHasher>();
        mockPasswordHasher.Setup(x => x.Hash(It.IsAny<string>())).Returns("hashed_password");

        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(Guid.NewGuid());

        return new CreateUserCommandHandler(
            context,
            passwordHasher ?? mockPasswordHasher.Object,
            tenantService ?? mockTenantService.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessWithUserId()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        var command = new CreateUserCommand(
            "test@example.com",
            "Password123!",
            "John",
            "Doe",
            "+5491112345678",
            UserRole.Empleado);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == result.Value);
        user.Should().NotBeNull();
        user!.FirstName.Should().Be("John");
        user.LastName.Should().Be("Doe");
        user.Email.Should().Be(new Email("test@example.com"));
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ReturnsEmailNotUniqueError()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);
        var handler = CreateHandler(context, tenantService: mockTenantService.Object);

        // Create first user
        var firstUser = new User(dealerId, "duplicate@example.com", "First", "User", "hash1");
        context.Users.Add(firstUser);
        await context.SaveChangesAsync();

        var command = new CreateUserCommand(
            "duplicate@example.com",
            "Password123!",
            "Second",
            "User",
            null,
            UserRole.Empleado);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.EmailNotUnique);
    }

    [Fact]
    public async Task Handle_WithoutPhone_SetsPhoneToNull()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        var command = new CreateUserCommand(
            "nophone@example.com",
            "Password123!",
            "No",
            "Phone",
            null,
            UserRole.Empleado);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == result.Value);
        user!.Phone.Should().BeNull();
    }

    [Fact]
    public async Task Handle_DifferentDealers_SameEmailAllowed()
    {
        using var context = CreateContext();
        var dealerId1 = Guid.NewGuid();
        var dealerId2 = Guid.NewGuid();

        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId1);

        var handler = CreateHandler(context, tenantService: mockTenantService.Object);

        var command = new CreateUserCommand(
            "shared@example.com",
            "Password123!",
            "Shared",
            "Email",
            null,
            UserRole.Empleado);

        // Create user in different dealer
        var differentDealerUser = new User(dealerId2, "shared@example.com", "Other", "Dealer", "hash2");
        context.Users.Add(differentDealerUser);
        await context.SaveChangesAsync();

        // Handler should succeed for dealer 1 since the email belongs to dealer 2
        var result = await handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_TrimsWhitespace_FromNameAndEmail()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        var command = new CreateUserCommand(
            "  spaces@example.com  ",
            "Password123!",
            "  John  ",
            "  Doe  ",
            null,
            UserRole.Empleado);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == result.Value);
        user!.FirstName.Should().Be("John");
        user.LastName.Should().Be("Doe");
        user.Email.Should().Be(new Email("spaces@example.com"));
    }

    [Fact]
    public async Task Handle_EmailIsLowercased()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        var command = new CreateUserCommand(
            "UPPERCASE@EXAMPLE.COM",
            "Password123!",
            "Test",
            "User",
            null,
            UserRole.Empleado);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == result.Value);
        user!.Email.Value.Should().Be("uppercase@example.com");
    }
}