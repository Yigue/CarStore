using Application.Abstractions.Authentication;
using Application.Users.Preferences;
using Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Users;

/// <summary>
/// sfc B5.3 / ADR C21: GET/PUT users/me/preferences. A user with nothing saved gets the defaults; a change persists and is
/// read back after reloading; values outside what the UI offers are rejected.
/// </summary>
public class UpdatePreferencesCommandHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static DbContextOptions<TestApplicationDbContext> Options(string db) =>
        new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(db).Options;

    private static IUserContext User()
    {
        var user = new Mock<IUserContext>();
        user.SetupGet(u => u.UserId).Returns(UserId);
        return user.Object;
    }

    private static UpdateMyPreferencesCommand Command(
        string sort = "year_desc", string view = "list", int perPage = 48,
        bool newMessages = false, bool priceDrops = true, bool newsletter = false) =>
        new(sort, view, perPage, newMessages, priceDrops, newsletter);

    [Fact]
    public async Task Get_Should_ReturnTheDefaults_WhenNothingWasSaved()
    {
        using var context = new TestApplicationDbContext(Options(Guid.NewGuid().ToString()));

        var result = await new GetMyPreferencesQueryHandler(context, User()).Handle(new GetMyPreferencesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new UserPreferencesResponse("price_asc", "grid", 12, true, false, true));
    }

    [Fact]
    public async Task Update_Should_Persist_AndBeReadBackAfterReloading()
    {
        string db = Guid.NewGuid().ToString();
        await using (var write = new TestApplicationDbContext(Options(db)))
        {
            (await new UpdateMyPreferencesCommandHandler(write, User()).Handle(Command(), CancellationToken.None))
                .IsSuccess.Should().BeTrue();
        }

        await using (var second = new TestApplicationDbContext(Options(db)))
        {
            // A second save updates the same row instead of adding another.
            (await new UpdateMyPreferencesCommandHandler(second, User()).Handle(Command(perPage: 24), CancellationToken.None))
                .IsSuccess.Should().BeTrue();
        }

        await using var read = new TestApplicationDbContext(Options(db));
        var result = await new GetMyPreferencesQueryHandler(read, User()).Handle(new GetMyPreferencesQuery(), CancellationToken.None);
        result.Value.Should().Be(new UserPreferencesResponse("year_desc", "list", 24, false, true, false));
        (await read.UserPreferences.CountAsync()).Should().Be(1);
        (await read.UserPreferences.SingleAsync()).SchemaVersion.Should().Be(UserPreferences.CurrentSchemaVersion);
    }

    [Theory]
    [InlineData("random", "grid", 12)]
    [InlineData("price_asc", "table", 12)]
    [InlineData("price_asc", "grid", 13)]
    public async Task Update_Should_RejectValuesTheUiDoesNotOffer(string sort, string view, int perPage)
    {
        using var context = new TestApplicationDbContext(Options(Guid.NewGuid().ToString()));

        var result = await new UpdateMyPreferencesCommandHandler(context, User())
            .Handle(Command(sort, view, perPage), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Preferences.Invalid");
        (await context.UserPreferences.CountAsync()).Should().Be(0);
    }
}
