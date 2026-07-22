using Microsoft.AspNetCore.Http;

namespace Cycles.Tests;

public sealed class CanonicalOidcOriginMiddlewareTests
{
    private const string CanonicalHost = "cycles.example.test";
    private const string ProxySecret = "test-proxy-secret-that-is-at-least-32-characters";

    [Fact]
    public async Task Authenticated_proxy_request_supplies_the_canonical_request_origin()
    {
        var nextCalled = false;
        var middleware = new CanonicalOidcOriginMiddleware(
            context =>
            {
                nextCalled = true;
                Assert.Equal(CanonicalHost, context.Request.Host.Value);
                Assert.Equal("https", context.Request.Scheme);
                Assert.False(context.Request.Headers.ContainsKey(CanonicalOidcOriginHeaders.ProxySecret));
                Assert.False(context.Request.Headers.ContainsKey("X-Forwarded-Host"));
                return Task.CompletedTask;
            },
            CanonicalHost,
            ProxySecret);
        var context = CreateContext("GET", "/app.html");
        context.Request.Headers[CanonicalOidcOriginHeaders.ProxySecret] = ProxySecret;
        context.Request.Headers["X-Forwarded-Host"] = CanonicalHost;
        context.Request.Headers["X-Forwarded-Proto"] = "https";

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Theory]
    [InlineData("wrong-secret-that-is-also-at-least-32-characters", 403)]
    [InlineData("", 503)]
    public async Task Canonical_proxy_request_without_the_expected_secret_is_refused(
        string suppliedSecret,
        int expectedStatus)
    {
        var middleware = new CanonicalOidcOriginMiddleware(
            _ => throw new InvalidOperationException("The refused request must not continue."),
            CanonicalHost,
            ProxySecret);
        var context = CreateContext("GET", "/app.html");
        if (suppliedSecret.Length != 0)
        {
            context.Request.Headers[CanonicalOidcOriginHeaders.ProxySecret] = suppliedSecret;
        }
        context.Request.Headers["X-Forwarded-Host"] = CanonicalHost;
        context.Request.Headers["X-Forwarded-Proto"] = "https";

        await middleware.InvokeAsync(context);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task Direct_safe_request_redirects_to_the_canonical_host()
    {
        var middleware = new CanonicalOidcOriginMiddleware(
            _ => throw new InvalidOperationException("The direct request must not continue."),
            CanonicalHost,
            ProxySecret);
        var context = CreateContext("GET", "/auth/external/login");
        context.Request.QueryString = new QueryString("?return=1");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status307TemporaryRedirect, context.Response.StatusCode);
        Assert.Equal(
            "https://cycles.example.test/auth/external/login?return=1",
            context.Response.Headers.Location);
    }

    [Fact]
    public async Task Direct_mutation_is_refused_while_health_remains_available()
    {
        var middleware = new CanonicalOidcOriginMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            },
            CanonicalHost,
            ProxySecret);
        var mutation = CreateContext("POST", "/auth/logout");
        var health = CreateContext("GET", "/health");

        await middleware.InvokeAsync(mutation);
        await middleware.InvokeAsync(health);

        Assert.Equal(StatusCodes.Status403Forbidden, mutation.Response.StatusCode);
        Assert.Equal(StatusCodes.Status204NoContent, health.Response.StatusCode);
    }

    private static DefaultHttpContext CreateContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }
}
