using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;

internal static class ApiAntiforgery
{
    internal const string EndpointPath = "/auth/antiforgery";
    internal const string HeaderName = "X-Cycles-Antiforgery";
    internal const string FormFieldName = "__RequestVerificationToken";
    internal const string ErrorCode = "antiforgeryFailed";
    internal const string DevelopmentCookieName = "cycles.antiforgery";
    internal const string SecureCookieName = "__Host-CyclesAntiforgery";

    public static IServiceCollection AddCyclesApiAntiforgery(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);

        var useSecureCookie = !environment.IsDevelopment();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = HeaderName;
            options.FormFieldName = FormFieldName;
            options.Cookie.Name = useSecureCookie
                ? SecureCookieName
                : DevelopmentCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.Path = "/";
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = useSecureCookie
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;
        });

        return services;
    }

    public static ApiAntiforgeryTokenResponse IssueToken(
        HttpContext httpContext,
        IAntiforgery antiforgery)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(antiforgery);

        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        var requestToken = tokens.RequestToken
            ?? throw new InvalidOperationException("The antiforgery service did not issue a request token.");

        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers.Pragma = "no-cache";
        httpContext.Response.Headers.Vary = "Cookie";
        return new ApiAntiforgeryTokenResponse(requestToken);
    }

    public static void ExpireTokenCookie(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var options = httpContext.RequestServices
            .GetRequiredService<IOptions<AntiforgeryOptions>>()
            .Value;
        var cookieName = options.Cookie.Name
            ?? throw new InvalidOperationException("The antiforgery cookie has no configured name.");
        httpContext.Response.Cookies.Delete(cookieName, options.Cookie.Build(httpContext));
    }

    public static RouteHandlerBuilder RequireCyclesAntiforgery(this RouteHandlerBuilder endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        endpoint.AddEndpointFilter<ApiAntiforgeryEndpointFilter>();
        endpoint.WithMetadata(ApiAntiforgeryRequiredMetadata.Instance);
        return endpoint;
    }

    public static RouteGroupBuilder RequireCyclesAntiforgery(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.AddEndpointFilter<ApiAntiforgeryEndpointFilter>();
        group.WithMetadata(ApiAntiforgeryRequiredMetadata.Instance);
        return group;
    }
}

internal sealed class ApiAntiforgeryEndpointFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return Results.Json(
                new ErrorResponse(
                    ApiAntiforgery.ErrorCode,
                    "The security token was missing or expired. Refresh and try again.",
                    Details: null,
                    TraceId: context.HttpContext.TraceIdentifier),
                statusCode: StatusCodes.Status400BadRequest);
        }

        return await next(context);
    }
}

internal sealed class ApiAntiforgeryRequiredMetadata
{
    public static ApiAntiforgeryRequiredMetadata Instance { get; } = new();

    private ApiAntiforgeryRequiredMetadata()
    {
    }
}

internal sealed record ApiAntiforgeryTokenResponse(string RequestToken);
