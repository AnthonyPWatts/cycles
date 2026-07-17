internal sealed class EdgeAssetRedirectMiddleware
{
    private static readonly HashSet<string> EdgeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".avif",
        ".gif",
        ".jpeg",
        ".jpg",
        ".mp4",
        ".png",
        ".svg",
        ".webm",
        ".webp"
    };

    private readonly RequestDelegate next;
    private readonly Uri edgeOrigin;

    public EdgeAssetRedirectMiddleware(RequestDelegate next, string edgeOrigin)
    {
        this.next = next;
        if (!Uri.TryCreate(edgeOrigin, UriKind.Absolute, out var parsedOrigin)
            || parsedOrigin.Scheme != Uri.UriSchemeHttps
            || parsedOrigin.AbsolutePath != "/"
            || !string.IsNullOrEmpty(parsedOrigin.Query)
            || !string.IsNullOrEmpty(parsedOrigin.Fragment))
        {
            throw new InvalidOperationException("Cycles:EdgeAssetOrigin must be an HTTPS origin without a path, query, or fragment.");
        }

        this.edgeOrigin = parsedOrigin;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if ((!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            || !IsEdgeAssetPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        var forwardedHost = context.Request.Headers["X-Forwarded-Host"].ToString()
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (string.Equals(forwardedHost, edgeOrigin.Authority, StringComparison.OrdinalIgnoreCase)
            || string.Equals(forwardedHost, edgeOrigin.Host, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            context.Response.ContentType = "text/plain; charset=utf-8";
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsync("Edge asset routing failed before the request reached the Azure origin.");
            return;
        }

        var location = $"{edgeOrigin.GetLeftPart(UriPartial.Authority)}{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
        context.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
        context.Response.Headers.Location = location;
        context.Response.Headers.CacheControl = "no-store";
    }

    internal static bool IsEdgeAssetPath(PathString path)
    {
        if (!path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWithSegments("/media", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return EdgeExtensions.Contains(Path.GetExtension(path.Value ?? string.Empty));
    }
}

internal static class EdgeAssetRedirectMiddlewareExtensions
{
    public static IApplicationBuilder UseEdgeAssetRedirect(this IApplicationBuilder app, string? edgeOrigin)
    {
        return string.IsNullOrWhiteSpace(edgeOrigin)
            ? app
            : app.UseMiddleware<EdgeAssetRedirectMiddleware>(edgeOrigin);
    }
}
