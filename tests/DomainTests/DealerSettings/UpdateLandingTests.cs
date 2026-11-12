using Domain.DealerSettings;
using SharedKernel;

namespace DomainTests.DealerSettings;

/// <summary>
/// CFG-06: a dealership rewrites its own landing page — history, mission, vision, values and the
/// hero carousel — instead of shipping every dealer the same hardcoded, unrelated copy.
/// </summary>
public class UpdateLandingTests
{
    private static Domain.DealerSettings.DealerSettings CreateDealer()
        => new(
            dealerId: Guid.NewGuid(),
            dealerName: "Test Dealer",
            contactEmail: "test@dealer.com");

    [Fact]
    public void UpdateLanding_SetsTheFreeTextFields()
    {
        var dealer = CreateDealer();

        dealer.UpdateLanding(
            historyText: "Fundada en 1990.",
            missionText: "Vender autos con transparencia.",
            visionText: "Ser líderes en la región.",
            valuesText: "Honestidad, compromiso.",
            carouselSlides: null);

        dealer.HistoryText.Should().Be("Fundada en 1990.");
        dealer.MissionText.Should().Be("Vender autos con transparencia.");
        dealer.VisionText.Should().Be("Ser líderes en la región.");
        dealer.ValuesText.Should().Be("Honestidad, compromiso.");
    }

    [Fact]
    public void UpdateLanding_TrimsTextAndTreatsWhitespaceAsNull()
    {
        var dealer = CreateDealer();

        dealer.UpdateLanding("  Historia con espacios  ", "   ", null, "", carouselSlides: null);

        dealer.HistoryText.Should().Be("Historia con espacios");
        dealer.MissionText.Should().BeNull("whitespace-only text is the same as not having entered anything");
        dealer.VisionText.Should().BeNull();
        dealer.ValuesText.Should().BeNull();
    }

    [Fact]
    public void UpdateLanding_Fails_WhenATextFieldExceedsTheLengthLimit()
    {
        var dealer = CreateDealer();
        var tooLong = new string('a', Domain.DealerSettings.DealerSettings.MaxLandingTextLength + 1);

        var act = () => dealer.UpdateLanding(tooLong, null, null, null, null);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void UpdateLanding_StoresTheCarouselAsRetrievableSlides()
    {
        var dealer = CreateDealer();
        var slides = new List<CarouselSlide>
        {
            new("https://cdn/1.jpg", "Bienvenidos", "Subtítulo", "Desc", "Ver más", "/catalogo"),
            new("https://cdn/2.jpg", "Ofertas", null, null, null, null),
        };

        dealer.UpdateLanding(null, null, null, null, slides);

        var stored = dealer.GetCarouselSlides();
        stored.Should().HaveCount(2);
        stored[0].Should().Be(slides[0]);
        stored[1].Should().Be(slides[1]);
    }

    [Fact]
    public void UpdateLanding_ClearsTheCarousel_WhenGivenAnEmptyList()
    {
        var dealer = CreateDealer();
        dealer.UpdateLanding(null, null, null, null, [new CarouselSlide("https://cdn/1.jpg", "X", null, null, null, null)]);

        dealer.UpdateLanding(null, null, null, null, []);

        dealer.GetCarouselSlides().Should().BeEmpty();
        dealer.CarouselSlidesJson.Should().BeNull("an empty carousel means 'use the built-in default slides', not an empty JSON array sitting in the column");
    }

    [Fact]
    public void UpdateLanding_Fails_WhenTheCarouselHasTooManySlides()
    {
        var dealer = CreateDealer();
        var tooMany = Enumerable.Range(0, Domain.DealerSettings.DealerSettings.MaxCarouselSlides + 1)
            .Select(i => new CarouselSlide($"https://cdn/{i}.jpg", $"Slide {i}", null, null, null, null))
            .ToList();

        var act = () => dealer.UpdateLanding(null, null, null, null, tooMany);

        act.Should().Throw<DomainException>();
        dealer.CarouselSlidesJson.Should().BeNull("nothing is written when the whole update is rejected");
    }

    [Fact]
    public void UpdateLanding_Fails_WhenASlideHasNoImage()
    {
        var dealer = CreateDealer();
        var slides = new List<CarouselSlide> { new("", "Título", null, null, null, null) };

        var act = () => dealer.UpdateLanding(null, null, null, null, slides);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void UpdateLanding_Fails_WhenASlideHasNoTitle()
    {
        var dealer = CreateDealer();
        var slides = new List<CarouselSlide> { new("https://cdn/1.jpg", "", null, null, null, null) };

        var act = () => dealer.UpdateLanding(null, null, null, null, slides);

        act.Should().Throw<DomainException>();
    }

    // The whole aggregate has to reject the call together, or a caller that reuses this instance
    // after catching the exception (as the application handler does) sees a HistoryText that was
    // never actually saved.
    [Fact]
    public void UpdateLanding_LeavesEveryFieldUntouched_WhenTheCarouselIsRejected()
    {
        var dealer = CreateDealer();
        dealer.UpdateLanding("Historia original", "Misión original", null, null, null);

        var badSlides = new List<CarouselSlide> { new("", "Sin imagen", null, null, null, null) };
        var act = () => dealer.UpdateLanding("Historia nueva", "Misión nueva", null, null, badSlides);

        act.Should().Throw<DomainException>();
        dealer.HistoryText.Should().Be("Historia original");
        dealer.MissionText.Should().Be("Misión original");
    }

    [Fact]
    public void GetCarouselSlides_ReturnsEmpty_WhenNothingWasEverSet()
    {
        var dealer = CreateDealer();

        dealer.GetCarouselSlides().Should().BeEmpty();
    }

}
