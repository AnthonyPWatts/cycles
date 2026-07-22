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

    [Fact]
    public void Public_privacy_page_explains_the_oidc_identity_link()
    {
        var landing = ReadLandingAsset("index.html");
        var privacy = ReadLandingAsset("privacy.html");

        Assert.Contains("href=\"/privacy.html\"", landing, StringComparison.Ordinal);
        Assert.Contains("verified email", privacy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("issuer and subject", privacy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not receive your Google password", privacy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cloudflare", privacy, StringComparison.Ordinal);
        Assert.Contains("Microsoft Azure", privacy, StringComparison.Ordinal);
    }

    private static string ReadLandingAsset(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Landing", fileName));
}
