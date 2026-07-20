using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace Cycles.Tests;

public sealed class ApiAntiforgeryTests
{
    private static readonly Regex RouteMappingPattern = new(
        @"(?<receiver>app|selectedGameRoutes)\.Map(?<method>Get|Post|Put|Patch|Delete)\s*\(",
        RegexOptions.CultureInvariant);

    [Theory]
    [InlineData("Development", ApiAntiforgery.DevelopmentCookieName, CookieSecurePolicy.SameAsRequest)]
    [InlineData("Production", ApiAntiforgery.SecureCookieName, CookieSecurePolicy.Always)]
    public void Registration_configures_the_shared_header_form_and_cookie_policy(
        string environmentName,
        string expectedCookieName,
        CookieSecurePolicy expectedSecurePolicy)
    {
        using var services = CreateServices(environmentName);

        var options = services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;

        Assert.Equal(ApiAntiforgery.HeaderName, options.HeaderName);
        Assert.Equal(ApiAntiforgery.FormFieldName, options.FormFieldName);
        Assert.Equal(expectedCookieName, options.Cookie.Name);
        Assert.Equal(expectedSecurePolicy, options.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, options.Cookie.SameSite);
        Assert.Equal("/", options.Cookie.Path);
        Assert.True(options.Cookie.HttpOnly);
        Assert.True(options.Cookie.IsEssential);
    }

    [Fact]
    public void Token_endpoint_contract_issues_only_the_request_token_and_disables_caching()
    {
        var antiforgery = new TrackingAntiforgery(requestToken: "request-token");
        var context = new DefaultHttpContext();

        var response = ApiAntiforgery.IssueToken(context, antiforgery);

        Assert.Equal("request-token", response.RequestToken);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal("no-cache", context.Response.Headers.Pragma);
        Assert.Equal("Cookie", context.Response.Headers.Vary);
        Assert.Equal(1, antiforgery.IssueCount);
    }

    [Fact]
    public async Task Endpoint_filter_validates_before_invoking_the_handler()
    {
        var antiforgery = new TrackingAntiforgery(requestToken: "request-token");
        var filter = new ApiAntiforgeryEndpointFilter(antiforgery);
        var context = new DefaultHttpContext();
        var nextCalled = false;

        var result = await filter.InvokeAsync(
            EndpointFilterInvocationContext.Create(context),
            _ =>
            {
                nextCalled = true;
                return ValueTask.FromResult<object?>("accepted");
            });

        Assert.Equal("accepted", result);
        Assert.True(nextCalled);
        Assert.Equal(1, antiforgery.ValidationCount);
    }

    [Fact]
    public async Task Endpoint_filter_rejects_an_invalid_token_without_invoking_the_handler()
    {
        var antiforgery = new TrackingAntiforgery(
            requestToken: "request-token",
            validationFailure: new AntiforgeryValidationException("invalid"));
        var filter = new ApiAntiforgeryEndpointFilter(antiforgery);
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-antiforgery"
        };
        var nextCalled = false;

        var result = await filter.InvokeAsync(
            EndpointFilterInvocationContext.Create(context),
            _ =>
            {
                nextCalled = true;
                return ValueTask.FromResult<object?>("should-not-run");
            });

        Assert.False(nextCalled);
        Assert.Equal(1, antiforgery.ValidationCount);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var error = Assert.IsType<ErrorResponse>(valueResult.Value);
        Assert.Equal(ApiAntiforgery.ErrorCode, error.Code);
        Assert.Equal("trace-antiforgery", error.TraceId);
        Assert.DoesNotContain("invalid", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Expiring_the_token_uses_the_configured_cookie_identity()
    {
        using var services = CreateServices(Environments.Production);
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };

        ApiAntiforgery.ExpireTokenCookie(context);

        var setCookie = Assert.Single(context.Response.Headers.SetCookie);
        Assert.StartsWith($"{ApiAntiforgery.SecureCookieName}=;", setCookie);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Every_declared_api_mutation_requires_antiforgery_and_logout_is_never_get()
    {
        var program = ReadApiSource("Program.cs");
        var routes = RouteMappingPattern.Matches(program).Cast<Match>().ToArray();
        var mutations = routes
            .Where(route => route.Groups["method"].Value is not "Get")
            .ToArray();

        Assert.NotEmpty(mutations);
        foreach (var mutation in mutations)
        {
            var nextRoute = routes.FirstOrDefault(route => route.Index > mutation.Index);
            var end = nextRoute?.Index ?? program.Length;
            var mapping = program[mutation.Index..end];
            Assert.True(
                Regex.Matches(mapping, @"\.RequireCyclesAntiforgery\s*\(\s*\)").Count == 1,
                $"{mutation.Groups["receiver"].Value}.Map{mutation.Groups["method"].Value} at source offset {mutation.Index} must declare exactly one antiforgery requirement before the next route.");
        }

        Assert.Equal(
            mutations.Length,
            Regex.Matches(program, @"\.RequireCyclesAntiforgery\s*\(\s*\)").Count);
        Assert.DoesNotMatch(
            new Regex("(?:app|selectedGameRoutes)\\.MapGet\\s*\\(\\s*\"/auth/logout\"", RegexOptions.CultureInvariant),
            program);
    }

    private static ServiceProvider CreateServices(string environmentName)
    {
        var services = new ServiceCollection();
        services.AddCyclesApiAntiforgery(new TestHostEnvironment(environmentName));
        return services.BuildServiceProvider();
    }

    private static string ReadApiSource(string path) =>
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "OnlineSource",
            "Api",
            path));

    private sealed class TrackingAntiforgery(
        string requestToken,
        AntiforgeryValidationException? validationFailure = null) : IAntiforgery
    {
        public int IssueCount { get; private set; }

        public int ValidationCount { get; private set; }

        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext)
        {
            IssueCount += 1;
            return GetTokens(httpContext);
        }

        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) =>
            new(requestToken, "cookie-token", ApiAntiforgery.FormFieldName, ApiAntiforgery.HeaderName);

        public Task<bool> IsRequestValidAsync(HttpContext httpContext) =>
            Task.FromResult(validationFailure is null);

        public Task ValidateRequestAsync(HttpContext httpContext)
        {
            ValidationCount += 1;
            return validationFailure is null
                ? Task.CompletedTask
                : Task.FromException(validationFailure);
        }

        public void SetCookieTokenAndHeader(HttpContext httpContext)
        {
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Cycles.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
