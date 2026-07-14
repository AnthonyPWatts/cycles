using Cycles.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Text;

namespace Cycles.Tests;

public sealed class DashboardAccessMiddlewareTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/health")]
    [InlineData("/auth/error")]
    public async Task Public_and_authentication_routes_are_not_challenged(string path)
    {
        var nextCalled = false;
        var middleware = new DashboardAccessMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, protectDashboard: true);
        var context = CreateContext(path);

        await middleware.InvokeAsync(context, new InMemoryStore(TestState.CreateSingleEmpireState()));

        Assert.True(nextCalled);
        Assert.Null(context.RequestServices.GetRequiredService<RecordingAuthenticationService>().ChallengedScheme);
    }

    [Fact]
    public async Task Anonymous_dashboard_request_starts_external_challenge()
    {
        var middleware = new DashboardAccessMiddleware(_ => Task.CompletedTask, protectDashboard: true);
        var context = CreateContext("/app.html");

        await middleware.InvokeAsync(context, new InMemoryStore(TestState.CreateSingleEmpireState()));

        var authentication = context.RequestServices.GetRequiredService<RecordingAuthenticationService>();
        Assert.Equal(CyclesAuthenticationSchemes.OpenIdConnect, authentication.ChallengedScheme);
        Assert.Equal("/app.html", authentication.ChallengeProperties?.RedirectUri);
    }

    [Fact]
    public async Task Admitted_authenticated_player_receives_dashboard()
    {
        var state = TestState.CreateSingleEmpireState();
        var player = state.Players.Single();
        var nextCalled = false;
        var middleware = new DashboardAccessMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, protectDashboard: true);
        var context = CreateContext("/app.html", player.PlayerId);

        await middleware.InvokeAsync(context, new InMemoryStore(state));

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Authenticated_identity_without_local_admission_is_refused()
    {
        var middleware = new DashboardAccessMiddleware(_ => Task.CompletedTask, protectDashboard: true);
        var context = CreateContext("/app.html", Guid.NewGuid());

        await middleware.InvokeAsync(context, new InMemoryStore(TestState.CreateSingleEmpireState()));

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        Assert.Contains("not admitted", await reader.ReadToEndAsync(), StringComparison.Ordinal);
    }

    private static DefaultHttpContext CreateContext(string path, Guid? playerId = null)
    {
        var authentication = new RecordingAuthenticationService();
        var services = new ServiceCollection()
            .AddSingleton(authentication)
            .AddSingleton<IAuthenticationService>(authentication)
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (playerId.HasValue)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(CyclesClaimTypes.PlayerId, playerId.Value.ToString("D"))],
                CyclesAuthenticationSchemes.Cookie));
        }

        return context;
    }

    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public string? ChallengedScheme { get; private set; }
        public AuthenticationProperties? ChallengeProperties { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            ChallengedScheme = scheme;
            ChallengeProperties = properties;
            return Task.CompletedTask;
        }

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }

    private sealed class InMemoryStore(GameState state) : IGameStateStore
    {
        public string Description => "test";
        public GameState LoadOrCreate() => state;
        public T Update<T>(Func<GameState, T> update) => update(state);
        public TickResult RunTick(DateTimeOffset now) => throw new NotSupportedException();
        public void Replace(GameState replacement) => throw new NotSupportedException();
    }
}
