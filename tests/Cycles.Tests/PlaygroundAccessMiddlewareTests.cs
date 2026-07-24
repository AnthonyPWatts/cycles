using System.Text;
using Microsoft.AspNetCore.Http;

namespace Cycles.Tests;

public sealed class PlaygroundAccessMiddlewareTests
{
    private const string AccessCode = "correct-horse-battery-staple-2026";

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/privacy.html")]
    [InlineData("/site.css")]
    [InlineData("/media/cycles-promo.mp4")]
    [InlineData("/media/cycles-promo-30s.mp4")]
    [InlineData("/media/cycles-promo-poster.jpg")]
    [InlineData("/media/promo/concept-treaty-gate-battle.png")]
    [InlineData("/media/navigation-backgrounds/command.webp")]
    [InlineData("/assets/galaxy/galaxy-overview.webp")]
    [InlineData("/assets/galaxy/twin-reaches-inner-reach.webp")]
    [InlineData("/health")]
    public async Task PublicLandingRequest_RemainsAvailableWithoutAPlaygroundCookie(string path)
    {
        var nextCalled = false;
        var middleware = new PlaygroundAccessMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, AccessCode);
        var context = CreateContext("GET", path);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Theory]
    [InlineData("/app.html")]
    [InlineData("/app.js")]
    [InlineData("/styles.css")]
    [InlineData("/office-mode.css")]
    [InlineData("/media/PROMO-PRODUCTION.md")]
    [InlineData("/auth/login")]
    [InlineData("/galaxy")]
    public async Task UnauthenticatedApplicationRequest_ShowsTheAccessForm(string path)
    {
        var middleware = new PlaygroundAccessMiddleware(_ => Task.CompletedTask, AccessCode);
        var context = CreateContext("GET", path);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Contains("Cycles trusted playground", await ReadResponseAsync(context));
    }

    [Fact]
    public async Task CorrectCode_SetsAHostOnlySecureCookieAndRedirects()
    {
        var middleware = new PlaygroundAccessMiddleware(_ => Task.CompletedTask, AccessCode);
        var context = CreateContext("POST", PlaygroundAccessMiddleware.LoginPath);
        var form = $"accessCode={Uri.EscapeDataString(AccessCode)}";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(form));

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
        Assert.Equal("/app.html", context.Response.Headers.Location);
        var setCookie = Assert.Single(context.Response.Headers.SetCookie);
        Assert.Contains(PlaygroundAccessMiddleware.CookieName, setCookie);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(AccessCode, setCookie);
    }

    [Fact]
    public async Task ValidCookie_AllowsTheRequest()
    {
        var nextCalled = false;
        var middleware = new PlaygroundAccessMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, AccessCode);
        var context = CreateContext("GET", "/app.html");
        var token = PlaygroundAccessMiddleware.CreateCookieToken(AccessCode);
        context.Request.Headers.Cookie = $"{PlaygroundAccessMiddleware.CookieName}={token}";

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public void IncorrectCode_IsRejected()
    {
        var middleware = new PlaygroundAccessMiddleware(_ => Task.CompletedTask, AccessCode);

        Assert.False(middleware.IsValidAccessCode("not-the-code"));
        Assert.True(middleware.IsValidAccessCode(AccessCode));
    }

    private static DefaultHttpContext CreateContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadResponseAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }
}
