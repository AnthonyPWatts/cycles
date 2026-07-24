internal static class StaticAssetCachePolicy
{
    private static readonly HashSet<string> RapidDevelopmentExtensions = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ".css",
        ".html",
        ".js",
        ".json"
    };

    internal static void Apply(HttpContext context, string fileName)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (RapidDevelopmentExtensions.Contains(Path.GetExtension(fileName)))
        {
            // The application shell changes frequently during playground
            // development. Browsers may retain a validator, but every use must
            // revalidate and Cloudflare must not retain a stale response.
            context.Response.Headers.CacheControl = "no-cache, max-age=0, must-revalidate";
            context.Response.Headers["CDN-Cache-Control"] = "no-store";
            context.Response.Headers["Cloudflare-CDN-Cache-Control"] = "no-store";
            return;
        }

        if (!PlaygroundAccessMiddleware.IsPublicStaticAsset(context.Request.Path))
        {
            return;
        }

        var isVersioned = context.Request.Query.ContainsKey("v");
        context.Response.Headers.CacheControl = isVersioned
            ? "public, max-age=86400, immutable"
            : "public, max-age=3600";
        context.Response.Headers["Cloudflare-CDN-Cache-Control"] = isVersioned
            ? "public, max-age=604800, immutable"
            : "public, max-age=86400";
    }
}
