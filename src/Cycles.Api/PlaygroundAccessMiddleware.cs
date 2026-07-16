using System.Security.Cryptography;
using System.Text;

internal static class PlaygroundAccessExtensions
{
    public static IApplicationBuilder UsePlaygroundAccess(this IApplicationBuilder app, string? accessCode)
    {
        if (string.IsNullOrWhiteSpace(accessCode))
        {
            return app;
        }

        if (accessCode.Length < 24)
        {
            throw new InvalidOperationException("The playground access code must contain at least 24 characters.");
        }

        return app.UseMiddleware<PlaygroundAccessMiddleware>(accessCode);
    }
}

internal sealed class PlaygroundAccessMiddleware(RequestDelegate next, string accessCode)
{
    internal const string CookieName = "__Host-CyclesPlaygroundAccess";
    internal const string LoginPath = "/playground-access";

    private readonly byte[] accessCodeDigest = Digest(accessCode);
    private readonly string cookieToken = CreateCookieToken(accessCode);

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsPublicPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (context.Request.Path.Equals(LoginPath, StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsPost(context.Request.Method))
        {
            await SignInAsync(context);
            return;
        }

        if (HasValidCookie(context.Request))
        {
            await next(context);
            return;
        }

        await WriteSignInPageAsync(context, null);
    }

    internal bool IsValidAccessCode(string? candidate) =>
        candidate is not null
        && CryptographicOperations.FixedTimeEquals(accessCodeDigest, Digest(candidate));

    internal static string CreateCookieToken(string accessCode)
    {
        var digest = Digest($"cycles-playground-cookie:{accessCode}");
        return Convert.ToBase64String(digest)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private async Task SignInAsync(HttpContext context)
    {
        if (!context.Request.HasFormContentType)
        {
            await WriteSignInPageAsync(context, "Enter the access code supplied by the host.");
            return;
        }

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        if (!IsValidAccessCode(form["accessCode"].FirstOrDefault()))
        {
            await WriteSignInPageAsync(context, "That access code was not recognised.");
            return;
        }

        context.Response.Cookies.Append(CookieName, cookieToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            MaxAge = TimeSpan.FromDays(7),
            IsEssential = true
        });
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = "/app.html";
    }

    private bool HasValidCookie(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue(CookieName, out var candidate))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(cookieToken);
        var candidateBytes = Encoding.UTF8.GetBytes(candidate);
        return expectedBytes.Length == candidateBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, candidateBytes);
    }

    internal static bool IsPublicStaticAsset(PathString path) =>
        path.Equals("/site.css", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/media/cycles-promo-30s.mp4", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/media/cycles-promo-poster.jpg", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/media/promo", StringComparison.OrdinalIgnoreCase);

    private static bool IsPublicPath(PathString path) =>
        path.Equals("/", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/index.html", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/health", StringComparison.OrdinalIgnoreCase)
        || IsPublicStaticAsset(path);

    private static async Task WriteSignInPageAsync(HttpContext context, string? error)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        var errorHtml = error is null ? string.Empty : $"<p class=\"error\">{error}</p>";
        await context.Response.WriteAsync($$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Cycles playground access</title>
              <style>
                :root { color-scheme: dark; font-family: system-ui, sans-serif; }
                body { margin: 0; min-height: 100vh; display: grid; place-items: center; background: #080d18; color: #edf4ff; }
                main { width: min(28rem, calc(100% - 3rem)); padding: 2rem; border: 1px solid #2e4566; border-radius: 1rem; background: #101a2a; box-shadow: 0 1.5rem 4rem #0008; }
                h1 { margin-top: 0; font-size: 1.6rem; }
                p { color: #b9c8dc; line-height: 1.5; }
                label { display: block; margin: 1.5rem 0 .5rem; font-weight: 700; }
                input, button { box-sizing: border-box; width: 100%; min-height: 3rem; border-radius: .6rem; font: inherit; }
                input { border: 1px solid #58739a; padding: .7rem .85rem; background: #07101f; color: #fff; }
                button { margin-top: 1rem; border: 0; background: #68d9ff; color: #06101d; font-weight: 800; cursor: pointer; }
                .error { color: #ffb4b4; }
              </style>
            </head>
            <body>
              <main>
                <h1>Cycles trusted playground</h1>
                <p>This development build is limited to invited play-testers.</p>
                {{errorHtml}}
                <form method="post" action="{{LoginPath}}">
                  <label for="accessCode">Access code</label>
                  <input id="accessCode" name="accessCode" type="password" autocomplete="current-password" required autofocus>
                  <button type="submit">Enter playground</button>
                </form>
              </main>
            </body>
            </html>
            """, context.RequestAborted);
    }

    private static byte[] Digest(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
