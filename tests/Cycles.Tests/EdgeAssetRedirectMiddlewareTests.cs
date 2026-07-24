using Microsoft.AspNetCore.Http;

namespace Cycles.Tests;

public sealed class EdgeAssetRedirectMiddlewareTests
{
    private const string EdgeOrigin = "https://cycles.anthonypwatts.co.uk";

    [Theory]
    [InlineData("/media/cycles-promo.mp4")]
    [InlineData("/media/cycles-promo-30s.mp4")]
    [InlineData("/media/navigation-backgrounds/command.webp")]
    [InlineData("/assets/galaxy/galaxy-overview.webp")]
    [InlineData("/assets/galaxy/twin-reaches-overview.webp")]
    public async Task DirectOriginMediaRequest_RedirectsToTheConfiguredEdge(string path)
    {
        var nextCalled = false;
        var middleware = new EdgeAssetRedirectMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, EdgeOrigin);
        var context = CreateContext("GET", path, "?v=20260716-1");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status307TemporaryRedirect, context.Response.StatusCode);
        Assert.Equal($"{EdgeOrigin}{path}?v=20260716-1", context.Response.Headers.Location);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task ProxiedEdgeMediaRequest_FailsWithoutRedirectingBackToTheEdge()
    {
        var middleware = new EdgeAssetRedirectMiddleware(_ => Task.CompletedTask, EdgeOrigin);
        var context = CreateContext("GET", "/media/cycles-promo.mp4");
        context.Request.Headers["X-Forwarded-Host"] = "cycles.anthonypwatts.co.uk";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status502BadGateway, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
    }

    [Theory]
    [InlineData("GET", "/app.html")]
    [InlineData("GET", "/app.js")]
    [InlineData("GET", "/styles.css")]
    [InlineData("GET", "/office-mode.css")]
    [InlineData("GET", "/media/PROMO-PRODUCTION.md")]
    [InlineData("POST", "/media/cycles-promo.mp4")]
    public async Task NonEdgeRequest_ContinuesThroughTheApplicationPipeline(string method, string path)
    {
        var nextCalled = false;
        var middleware = new EdgeAssetRedirectMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, EdgeOrigin);
        var context = CreateContext(method, path);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public void NonHttpsOrPathBearingOrigin_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new EdgeAssetRedirectMiddleware(_ => Task.CompletedTask, "http://cycles.example"));
        Assert.Throws<InvalidOperationException>(() => new EdgeAssetRedirectMiddleware(_ => Task.CompletedTask, "https://cycles.example/media"));
    }

    private static DefaultHttpContext CreateContext(string method, string path, string query = "")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        return context;
    }
}
