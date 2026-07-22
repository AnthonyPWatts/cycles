using System.Security.Cryptography;
using System.Text;

internal static class CanonicalOidcOriginHeaders
{
    public const string ProxySecret = "X-Cycles-Proxy-Secret";
}

internal sealed class CanonicalOidcOriginMiddleware(
    RequestDelegate next,
    string canonicalHost,
    string proxySecret)
{
    private readonly HostString canonicalHost = new(canonicalHost);
    private readonly byte[] proxySecretHash = SHA256.HashData(Encoding.UTF8.GetBytes(proxySecret));

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            context.Request.Headers.Remove(CanonicalOidcOriginHeaders.ProxySecret);
            await next(context);
            return;
        }

        var suppliedSecret = context.Request.Headers[CanonicalOidcOriginHeaders.ProxySecret].ToString();
        if (suppliedSecret.Length != 0)
        {
            var suppliedSecretHash = SHA256.HashData(Encoding.UTF8.GetBytes(suppliedSecret));
            if (!CryptographicOperations.FixedTimeEquals(proxySecretHash, suppliedSecretHash))
            {
                await Refuse(context, StatusCodes.Status403Forbidden, "The origin proxy credential is invalid.");
                return;
            }

            var forwardedHost = SingleHeaderValue(context, "X-Forwarded-Host");
            var forwardedProto = SingleHeaderValue(context, "X-Forwarded-Proto");
            if (!string.Equals(forwardedHost, canonicalHost.Value, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(forwardedProto, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                await Refuse(context, StatusCodes.Status400BadRequest, "The canonical proxy headers are invalid.");
                return;
            }

            context.Request.Host = canonicalHost;
            context.Request.Scheme = Uri.UriSchemeHttps;
            context.Request.Headers.Remove(CanonicalOidcOriginHeaders.ProxySecret);
            context.Request.Headers.Remove("X-Forwarded-Host");
            context.Request.Headers.Remove("X-Forwarded-Proto");
            await next(context);
            return;
        }

        if (string.Equals(
                SingleHeaderValue(context, "X-Forwarded-Host"),
                canonicalHost.Value,
                StringComparison.OrdinalIgnoreCase))
        {
            await Refuse(
                context,
                StatusCodes.Status503ServiceUnavailable,
                "The canonical proxy is not configured to authenticate with the origin.");
            return;
        }

        if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
            context.Response.Headers.Location =
                $"https://{canonicalHost.Value}{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
            context.Response.Headers.CacheControl = "no-store";
            return;
        }

        await Refuse(context, StatusCodes.Status403Forbidden, "Direct origin requests are not accepted.");
    }

    private static string? SingleHeaderValue(HttpContext context, string headerName)
    {
        var value = context.Request.Headers[headerName].ToString();
        return value.Length != 0 && !value.Contains(',') ? value.Trim() : null;
    }

    private static async Task Refuse(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.WriteAsync(message, context.RequestAborted);
    }
}

internal static class CanonicalOidcOriginExtensions
{
    public static IApplicationBuilder UseCanonicalOidcOrigin(
        this IApplicationBuilder app,
        ExternalAuthenticationOptions options) =>
        app.UseMiddleware<CanonicalOidcOriginMiddleware>(
            options.CanonicalHost,
            options.ProxySecret);
}
