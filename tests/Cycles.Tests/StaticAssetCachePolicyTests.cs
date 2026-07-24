using Microsoft.AspNetCore.Http;

namespace Cycles.Tests;

public sealed class StaticAssetCachePolicyTests
{
    [Theory]
    [InlineData("/app.html", "app.html")]
    [InlineData("/app.js", "app.js")]
    [InlineData("/styles.css", "styles.css")]
    [InlineData("/office-mode.css", "office-mode.css")]
    [InlineData("/assets/admirals/catalogue.json", "catalogue.json")]
    public void Rapid_development_text_assets_always_revalidate_and_bypass_cdn_storage(
        string path,
        string fileName)
    {
        var context = CreateContext(path, "?v=stale-version");

        StaticAssetCachePolicy.Apply(context, fileName);

        Assert.Equal(
            "no-cache, max-age=0, must-revalidate",
            context.Response.Headers.CacheControl);
        Assert.Equal("no-store", context.Response.Headers["CDN-Cache-Control"]);
        Assert.Equal("no-store", context.Response.Headers["Cloudflare-CDN-Cache-Control"]);
    }

    [Fact]
    public void Versioned_public_binary_assets_keep_the_existing_immutable_policy()
    {
        var context = CreateContext("/assets/icons/refresh.svg", "?v=20260724-1");

        StaticAssetCachePolicy.Apply(context, "refresh.svg");

        Assert.Equal("public, max-age=86400, immutable", context.Response.Headers.CacheControl);
        Assert.Equal(
            "public, max-age=604800, immutable",
            context.Response.Headers["Cloudflare-CDN-Cache-Control"]);
        Assert.False(context.Response.Headers.ContainsKey("CDN-Cache-Control"));
    }

    [Fact]
    public void Unversioned_public_binary_assets_keep_the_existing_bounded_policy()
    {
        var context = CreateContext("/media/cycles-promo.mp4");

        StaticAssetCachePolicy.Apply(context, "cycles-promo.mp4");

        Assert.Equal("public, max-age=3600", context.Response.Headers.CacheControl);
        Assert.Equal(
            "public, max-age=86400",
            context.Response.Headers["Cloudflare-CDN-Cache-Control"]);
    }

    private static DefaultHttpContext CreateContext(string path, string? query = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (query is not null)
        {
            context.Request.QueryString = new QueryString(query);
        }

        return context;
    }
}
