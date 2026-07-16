namespace Cycles.Tests;

public sealed class LandingPageContractTests
{
    [Fact]
    public void Landing_page_uses_current_concept_art_without_the_prototype_map()
    {
        var html = ReadLandingAsset("index.html");
        var css = ReadLandingAsset("site.css");

        Assert.Contains("/media/promo/concept-gateway-transit.png", html);
        Assert.Contains("/media/promo/concept-gateway-transit.png", css);
        Assert.Contains("/media/promo/concept-treaty-gate-battle.png", html);
        Assert.Equal(2, html.Split(">Concept dramatisation<").Length - 1);

        Assert.DoesNotContain("heroScene", html);
        Assert.DoesNotContain("site.js", html);
        Assert.DoesNotContain("signal-map", html);
        Assert.DoesNotContain("signal-map", css);
        Assert.DoesNotContain("Archive Nine", html);
        Assert.DoesNotContain("Red Meridian", html);
        Assert.DoesNotContain("Silent Loom", html);
    }

    private static string ReadLandingAsset(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Landing", fileName));
}
