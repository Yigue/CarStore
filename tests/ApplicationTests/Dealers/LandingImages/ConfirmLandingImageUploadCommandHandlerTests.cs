using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Application.DealerSettings.Commands.UpdateDealerLanding;
using Application.Dealers.LandingImages.ConfirmLandingImageUpload;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;
using Xunit;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.UnitTests.Dealers.LandingImages;

/// <summary>
/// A5.1 / RN-035 (closes H11 for landing images too): the declared Content-Type is client-controlled,
/// so confirmation must trust only the object's real signature before it is ever usable in the
/// dealer's landing config.
/// </summary>
public class ConfirmLandingImageUploadCommandHandlerTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    private static readonly byte[] Exe = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00];

    private static readonly Guid DealerId = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext(Guid? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return tenantId is null
            ? new TestApplicationDbContext(options)
            : new TestApplicationDbContext(options, tenantId.Value);
    }

    private static Mock<ICurrentTenantService> MockTenant(Guid dealerId)
    {
        var mock = new Mock<ICurrentTenantService>();
        mock.SetupGet(t => t.HasTenant).Returns(true);
        mock.SetupGet(t => t.DealerId).Returns(dealerId);
        return mock;
    }

    /// <summary>
    /// Arranges storage so that ONLY the object key for <paramref name="keyOwnerDealerId"/> +
    /// <paramref name="imageId"/> resolves to <paramref name="head"/>. Any other key (e.g. the one a
    /// different tenant would compute) falls through Moq's default loose-mock behaviour and returns
    /// null — exactly like a real miss against object storage.
    /// </summary>
    private static Mock<IStorageService> ArrangeStorage(Guid keyOwnerDealerId, Guid imageId, string ext, byte[]? head)
    {
        string expectedKey = $"landing/{keyOwnerDealerId}/{imageId}.{ext}";

        var storage = new Mock<IStorageService>();
        storage.Setup(s => s.ReadHeadAsync(expectedKey, FileSignatureInspectorHeadLength, It.IsAny<CancellationToken>()))
            .ReturnsAsync(head);
        storage.Setup(s => s.GetPresignedUrlAsync(expectedKey, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Uri("https://example.com/landing/fake-image.png"));
        return storage;
    }

    private const int FileSignatureInspectorHeadLength = 16;

    [Fact]
    public async Task Handle_Should_Confirm_AndTheReturnedUrl_CanBePersistedIntoTheLandingConfig()
    {
        using var context = CreateContext(DealerId);
        var settings = new DealerSettingsEntity(DealerId, "Test Dealer", "dealer@test.com");
        context.DealerSettings.Add(settings);
        await context.SaveChangesAsync();

        var imageId = Guid.NewGuid();
        var storage = ArrangeStorage(DealerId, imageId, "png", Png);
        var handler = new ConfirmLandingImageUploadCommandHandler(
            storage.Object, MockTenant(DealerId).Object, new FakeDateTimeProvider());

        var command = new ConfirmLandingImageUploadCommand(imageId, "banner.png", "image/png", 2048);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Url.Should().Be("https://example.com/landing/fake-image.png");
        storage.Verify(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // The confirm step hands back a durable URL; the dealer's existing landing-update endpoint
        // (CFG-06) is what actually writes it into the landing config, as part of a complete slide.
        var landingHandler = new UpdateDealerLandingCommandHandler(context, MockTenant(DealerId).Object);
        var landingResult = await landingHandler.Handle(
            new UpdateDealerLandingCommand(
                HistoryText: null,
                MissionText: null,
                VisionText: null,
                ValuesText: null,
                CarouselSlides: [new CarouselSlideDto(result.Value.Url, "Bienvenidos", null, null, null, null)]),
            CancellationToken.None);

        landingResult.IsSuccess.Should().BeTrue();

        var persisted = await context.DealerSettings.SingleAsync(s => s.DealerId == DealerId);
        var slides = persisted.GetCarouselSlides();
        slides.Should().ContainSingle();
        slides[0].ImageUrl.Should().Be(result.Value.Url);
    }

    [Fact]
    public async Task Handle_Should_RejectAndDeleteObject_WhenMagicBytesDoNotMatchClaimedContentType()
    {
        using var context = CreateContext(DealerId);
        var imageId = Guid.NewGuid();
        // Claims image/jpeg + a .jpg extension, but the stored object is actually a renamed .exe —
        // a renamed .exe claiming to be a .jpg must not pass.
        var storage = ArrangeStorage(DealerId, imageId, "jpg", Exe);
        var handler = new ConfirmLandingImageUploadCommandHandler(
            storage.Object, MockTenant(DealerId).Object, new FakeDateTimeProvider());

        var command = new ConfirmLandingImageUploadCommand(imageId, "evil.jpg", "image/jpeg", 2048);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("LandingImage.InvalidSignature");
        storage.Verify(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Reject_WhenClaimedSizeExceedsMaximum()
    {
        using var context = CreateContext(DealerId);
        var imageId = Guid.NewGuid();
        var storage = ArrangeStorage(DealerId, imageId, "png", Png);
        var handler = new ConfirmLandingImageUploadCommandHandler(
            storage.Object, MockTenant(DealerId).Object, new FakeDateTimeProvider());

        long oversized = Application.Dealers.LandingImages.LandingImageUploadConstraints.MaxUploadSizeBytes + 1024;
        var command = new ConfirmLandingImageUploadCommand(imageId, "huge.png", "image/png", oversized);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("LandingImage.InvalidSize");
        result.Error.Type.Should().Be(ErrorType.Validation);
        storage.Verify(s => s.ReadHeadAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Reject_WhenCallerIsNotTheDealerThatOwnsTheUpload()
    {
        using var context = CreateContext(DealerId);
        var otherDealerId = Guid.NewGuid();
        var imageId = Guid.NewGuid();

        // DealerA's object actually exists in storage under landing/{DealerId}/{imageId}.png.
        var storage = ArrangeStorage(DealerId, imageId, "png", Png);

        // DealerB tries to confirm the SAME imageId. The handler must rebuild the key from DealerB's
        // own tenant (landing/{otherDealerId}/{imageId}.png), which was never stored — it can never
        // "find" DealerA's object no matter what the client claims.
        var handler = new ConfirmLandingImageUploadCommandHandler(
            storage.Object, MockTenant(otherDealerId).Object, new FakeDateTimeProvider());

        var command = new ConfirmLandingImageUploadCommand(imageId, "banner.png", "image/png", 2048);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("LandingImage.InvalidSignature");
    }
}
