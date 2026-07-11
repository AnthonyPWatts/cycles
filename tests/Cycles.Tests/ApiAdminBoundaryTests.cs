using Cycles.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cycles.Tests;

public sealed class ApiAdminBoundaryTests
{
    private static readonly IServiceProvider ResultServices = CreateResultServices();

    [Fact]
    public async Task Admin_tick_endpoint_runs_store_tick_for_admin()
    {
        var state = TestState.CreateSingleEmpireState();
        var admin = Assert.Single(state.Players);
        admin.Role = PlayerRole.Admin;
        var store = new InMemoryGameStateStore(state);
        var context = CreateAuthenticatedContext(admin);

        var result = ApiAdminEndpoints.RunTick(context, store, TestState.Now);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(1, store.RunTickCalls);
        Assert.Equal(1, state.GetActiveCycle()!.CurrentTickNumber);
    }

    [Fact]
    public async Task Admin_tick_endpoint_rejects_player()
    {
        var state = TestState.CreateSingleEmpireState();
        var player = Assert.Single(state.Players);
        var store = new InMemoryGameStateStore(state);
        var context = CreateAuthenticatedContext(player);

        var result = ApiAdminEndpoints.RunTick(context, store, TestState.Now);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(0, store.RunTickCalls);
        Assert.Equal(0, state.GetActiveCycle()!.CurrentTickNumber);
    }

    [Fact]
    public async Task Admin_tick_endpoint_allows_player_with_development_capability()
    {
        var state = TestState.CreateSingleEmpireState();
        var player = Assert.Single(state.Players);
        var store = new InMemoryGameStateStore(state);
        var context = CreateAuthenticatedContext(player);

        var result = ApiAdminEndpoints.RunTick(context, store, allowDevelopmentPlayer: true, TestState.Now);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(1, store.RunTickCalls);
        Assert.Equal(1, state.GetActiveCycle()!.CurrentTickNumber);
    }

    [Fact]
    public async Task Admin_tick_endpoint_requires_login()
    {
        var state = TestState.CreateSingleEmpireState();
        var store = new InMemoryGameStateStore(state);

        var result = ApiAdminEndpoints.RunTick(new DefaultHttpContext(), store, TestState.Now);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal(0, store.RunTickCalls);
    }

    private static DefaultHttpContext CreateAuthenticatedContext(Player player)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[DevelopmentAuth.HeaderName] = player.PlayerId.ToString("D");
        return context;
    }

    private static async Task<(int StatusCode, string Body)> ExecuteAsync(IResult result)
    {
        var context = new DefaultHttpContext { RequestServices = ResultServices };
        await using var body = new MemoryStream();
        context.Response.Body = body;
        await result.ExecuteAsync(context);
        body.Position = 0;
        using var reader = new StreamReader(body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private static IServiceProvider CreateResultServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        });
        return services.BuildServiceProvider();
    }

    private sealed class InMemoryGameStateStore(GameState state) : IGameStateStore
    {
        public string Description => "In-memory test state";

        public int RunTickCalls { get; private set; }

        public GameState LoadOrCreate() => state;

        public T Update<T>(Func<GameState, T> update) => update(state);

        public TickResult RunTick(DateTimeOffset now)
        {
            RunTickCalls++;
            var cycle = state.GetActiveCycle()
                ?? throw new InvalidOperationException("No active cycle exists.");
            return new TickEngine().RunTick(state, cycle.CycleId, now);
        }

        public void Replace(GameState replacement) => throw new NotSupportedException();
    }
}
