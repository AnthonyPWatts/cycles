using Cycles.Application;
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
        var context = AuthenticatedContext(actor);
        var roleCommands = new RecordingRoleCommandStore(command =>
            new AdminRoleCommandResult.Success(new AdminRoleChangeReceipt(
                Guid.NewGuid(),
                command.ActorPlayerId,
                command.TargetPlayerId,
                PlayerRole.Admin,
                AdminRoleAuditAction.Granted,
                command.ChangedAt)));

        var result = ApiAdminRoleEndpoints.Change(
            target.PlayerId,
            new AdminRoleChangeRequest("Private-alpha operator."),
            context,
            new InMemoryAccountQuery(ToSnapshot(actor)),
            roleCommands,
            grant: true,
            TestState.Now);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        var command = Assert.IsType<AdminRoleCommand>(roleCommands.Command);
        Assert.Equal(actor.PlayerId, command.ActorPlayerId);
        Assert.Equal(target.PlayerId, command.TargetPlayerId);
        Assert.Equal(AdminRoleChangeKind.Grant, command.Change);
        Assert.Equal("Private-alpha operator.", command.Reason);
        Assert.Contains("\"role\":\"admin\"", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ordinary_player_cannot_change_admin_roles()
    {
        var state = TestState.CreateSingleEmpireState();
        var actor = state.Players.Single();
        var target = new Player { Username = "target", Status = PlayerStatus.Active, CreatedAt = TestState.Now };
        var roleCommands = new RecordingRoleCommandStore(_ => new AdminRoleCommandResult.Forbidden());

        var result = ApiAdminRoleEndpoints.Change(
            target.PlayerId,
            new AdminRoleChangeRequest("Attempted escalation."),
            AuthenticatedContext(actor),
            new InMemoryAccountQuery(ToSnapshot(actor)),
            roleCommands,
            grant: true,
            TestState.Now);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Contains("\"code\":\"forbidden\"", response.Body, StringComparison.Ordinal);
        Assert.NotNull(roleCommands.Command);
    }

    [Theory]
    [InlineData(AdminRoleConflictReason.TargetUnavailable)]
    [InlineData(AdminRoleConflictReason.TargetIsAutomated)]
    [InlineData(AdminRoleConflictReason.AlreadyAdministrator)]
    [InlineData(AdminRoleConflictReason.NotAdministrator)]
    [InlineData(AdminRoleConflictReason.FinalActiveAdministrator)]
    public async Task Typed_role_conflicts_preserve_the_state_conflict_HTTP_contract(
        AdminRoleConflictReason reason)
    {
        var actor = TestState.CreateSingleEmpireState().Players.Single();
        actor.Role = PlayerRole.Admin;
        var roleCommands = new RecordingRoleCommandStore(_ => new AdminRoleCommandResult.Conflict(reason));

        var result = ApiAdminRoleEndpoints.Change(
            Guid.NewGuid(),
            new AdminRoleChangeRequest("Required for the operator roster."),
            AuthenticatedContext(actor),
            new InMemoryAccountQuery(ToSnapshot(actor)),
            roleCommands,
            grant: true,
            TestState.Now);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status409Conflict, response.StatusCode);
        Assert.Contains("\"code\":\"stateConflict\"", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Blank_role_reason_preserves_the_state_conflict_contract_without_calling_the_store()
    {
        var actor = TestState.CreateSingleEmpireState().Players.Single();
        actor.Role = PlayerRole.Admin;
        var roleCommands = new RecordingRoleCommandStore(_ =>
            throw new InvalidOperationException("Must not be called."));

        var result = ApiAdminRoleEndpoints.Change(
            Guid.NewGuid(),
            new AdminRoleChangeRequest("   "),
            AuthenticatedContext(actor),
            new InMemoryAccountQuery(ToSnapshot(actor)),
            roleCommands,
            grant: true,
            TestState.Now);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status409Conflict, response.StatusCode);
        Assert.Contains("\"code\":\"stateConflict\"", response.Body, StringComparison.Ordinal);
        Assert.Null(roleCommands.Command);
    }

    [Fact]
    public async Task Empty_target_preserves_the_state_conflict_contract_without_calling_the_store()
    {
        var actor = TestState.CreateSingleEmpireState().Players.Single();
        actor.Role = PlayerRole.Admin;
        var roleCommands = new RecordingRoleCommandStore(_ =>
            throw new InvalidOperationException("Must not be called."));

        var result = ApiAdminRoleEndpoints.Change(
            Guid.Empty,
            new AdminRoleChangeRequest("Required for the operator roster."),
            AuthenticatedContext(actor),
            new InMemoryAccountQuery(ToSnapshot(actor)),
            roleCommands,
            grant: true,
            TestState.Now);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status409Conflict, response.StatusCode);
        Assert.Contains("\"code\":\"stateConflict\"", response.Body, StringComparison.Ordinal);
        Assert.Null(roleCommands.Command);
    }

    [Fact]
    public async Task Busy_role_change_maps_to_a_stable_state_conflict()
    {
        var actor = TestState.CreateSingleEmpireState().Players.Single();
        actor.Role = PlayerRole.Admin;
        var roleCommands = new RecordingRoleCommandStore(_ => new AdminRoleCommandResult.Busy());

        var result = ApiAdminRoleEndpoints.Change(
            Guid.NewGuid(),
            new AdminRoleChangeRequest("Required for the operator roster."),
            AuthenticatedContext(actor),
            new InMemoryAccountQuery(ToSnapshot(actor)),
            roleCommands,
            grant: true,
            TestState.Now);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status409Conflict, response.StatusCode);
        Assert.Contains("\"code\":\"stateConflict\"", response.Body, StringComparison.Ordinal);
    }

    private static DefaultHttpContext AuthenticatedContext(Player player)
    {
        return TestHttpContextFactory.CreateAuthenticated(player);
    }

    private static PlayerAccountSnapshot ToSnapshot(Player player) =>
        new(
            player.PlayerId,
            player.Username,
            player.Kind,
            player.Role,
            player.Status,
            player.CreatedAt,
            player.LastLoginAt);

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

    private sealed class InMemoryAccountQuery(params PlayerAccountSnapshot[] players) : IPlayerAccountQuery
    {
        public PlayerAccountSnapshot? Get(Guid playerId) =>
            players.SingleOrDefault(player => player.PlayerId == playerId);
    }

    private sealed class RecordingRoleCommandStore(
        Func<AdminRoleCommand, AdminRoleCommandResult> execute) : IAdminRoleCommandStore
    {
        public AdminRoleCommand? Command { get; private set; }

        public AdminRoleCommandResult Change(AdminRoleCommand command)
        {
            Command = command;
            return execute(command);
        }
    }
}
