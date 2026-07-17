using Cycles.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Cycles.Tests;

public sealed class ApiAdminRoleBoundaryTests
{
    private static readonly IServiceProvider ResultServices = CreateResultServices();

    [Fact]
    public async Task Authenticated_admin_can_grant_role_with_reason_and_audit()
    {
        var state = TestState.CreateSingleEmpireState();
        var actor = state.Players.Single();
        actor.Role = PlayerRole.Admin;
        var target = new Player { Username = "target", Status = PlayerStatus.Active, CreatedAt = TestState.Now };
        state.Players.Add(target);
        var context = AuthenticatedContext(actor);

        var result = ApiAdminRoleEndpoints.Change(
            target.PlayerId,
            new AdminRoleChangeRequest("Private-alpha operator."),
            context,
            new InMemoryStore(state),
            grant: true,
            TestState.Now);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(PlayerRole.Admin, target.Role);
        var audit = Assert.Single(state.AdminRoleAuditRecords);
        Assert.Equal(actor.PlayerId, audit.ActorPlayerId);
        Assert.Equal(target.PlayerId, audit.TargetPlayerId);
        Assert.Equal("Private-alpha operator.", audit.Reason);
    }

    [Fact]
    public async Task Ordinary_player_cannot_change_admin_roles()
    {
        var state = TestState.CreateSingleEmpireState();
        var actor = state.Players.Single();
        var target = new Player { Username = "target", Status = PlayerStatus.Active, CreatedAt = TestState.Now };
        state.Players.Add(target);

        var result = ApiAdminRoleEndpoints.Change(
            target.PlayerId,
            new AdminRoleChangeRequest("Attempted escalation."),
            AuthenticatedContext(actor),
            new InMemoryStore(state),
            grant: true,
            TestState.Now);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Contains("\"code\":\"forbidden\"", response.Body, StringComparison.Ordinal);
        Assert.Equal(PlayerRole.Player, target.Role);
        Assert.Empty(state.AdminRoleAuditRecords);
    }

    private static DefaultHttpContext AuthenticatedContext(Player player)
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
        services.ConfigureHttpJsonOptions(options => ApiJson.Configure(options.SerializerOptions));
        return services.BuildServiceProvider();
    }

    private sealed class InMemoryStore(GameState state) : IGameStateStore
    {
        public string Description => "test";
        public GameState LoadOrCreate() => state;
        public T Update<T>(Func<GameState, T> update) => update(state);
        public TickResult RunTick(DateTimeOffset now) => throw new NotSupportedException();
        public TickResult? RunTickIfDue(DateTimeOffset now) => throw new NotSupportedException();
        public void Replace(GameState replacement) => throw new NotSupportedException();
    }
}
