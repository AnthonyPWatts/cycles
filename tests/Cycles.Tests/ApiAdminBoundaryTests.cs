using Cycles.Application;
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
        var game = new FocusedAdminFixture(state);
        var context = CreateAuthenticatedContext(admin);

        var result = ApiAdminEndpoints.RunTick(
            context,
            game.GameId,
            game.Requests,
            game,
            TestState.Now);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(1, game.RunTickCalls);
        Assert.NotNull(game.LastResolutionRequest);
        Assert.Equal(admin.PlayerId, game.LastResolutionRequest.Context.GameAccess.PlayerId);
        Assert.True(game.LastResolutionRequest.RequireAdminister);
        Assert.Equal(ExplicitCycleResolutionPolicy.Administrator, game.LastResolutionRequest.Policy);
        Assert.Equal(1, state.GetActiveCycle()!.CurrentTickNumber);
    }

    [Fact]
    public async Task Admin_tick_endpoint_rejects_player()
    {
        var state = TestState.CreateSingleEmpireState();
        var player = Assert.Single(state.Players);
        var game = new FocusedAdminFixture(state);
        var context = CreateAuthenticatedContext(player);

        var result = ApiAdminEndpoints.RunTick(
            context,
            game.GameId,
            game.Requests,
            game,
            TestState.Now);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Contains("\"code\":\"forbidden\"", response.Body, StringComparison.Ordinal);
        Assert.Equal(0, game.RunTickCalls);
        Assert.Equal(0, state.GetActiveCycle()!.CurrentTickNumber);
    }

    [Fact]
    public async Task Admin_tick_endpoint_allows_player_with_development_capability()
    {
        var state = TestState.CreateSingleEmpireState();
        var player = Assert.Single(state.Players);
        var game = new FocusedAdminFixture(state);
        var context = CreateAuthenticatedContext(player);

        var result = ApiAdminEndpoints.RunTick(
            context,
            game.GameId,
            game.Requests,
            game,
            allowDevelopmentPlayer: true,
            TestState.Now);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(1, game.RunTickCalls);
        Assert.NotNull(game.LastResolutionRequest);
        Assert.Equal(player.PlayerId, game.LastResolutionRequest.Context.GameAccess.PlayerId);
        Assert.False(game.LastResolutionRequest.RequireAdminister);
        Assert.Equal(ExplicitCycleResolutionPolicy.DevelopmentStandard, game.LastResolutionRequest.Policy);
        Assert.Equal(1, state.GetActiveCycle()!.CurrentTickNumber);
    }

    [Fact]
    public async Task Development_tick_endpoint_rejects_training_player_but_preserves_admin_authority()
    {
        var state = TestState.CreateSingleEmpireState();
        var player = Assert.Single(state.Players);
        var game = new FocusedAdminFixture(state);
        Assert.Single(state.Games).Purpose = GamePurpose.Training;

        var rejected = await ExecuteAsync(ApiAdminEndpoints.RunTick(
            CreateAuthenticatedContext(player),
            game.GameId,
            game.Requests,
            game,
            allowDevelopmentPlayer: true,
            TestState.Now));

        Assert.Equal(StatusCodes.Status403Forbidden, rejected.StatusCode);
        Assert.Contains("\"code\":\"forbidden\"", rejected.Body, StringComparison.Ordinal);
        Assert.Equal(0, game.RunTickCalls);

        player.Role = PlayerRole.Admin;
        var authorised = await ExecuteAsync(ApiAdminEndpoints.RunTick(
            CreateAuthenticatedContext(player),
            game.GameId,
            game.Requests,
            game,
            allowDevelopmentPlayer: true,
            TestState.Now));

        Assert.Equal(StatusCodes.Status200OK, authorised.StatusCode);
        Assert.Equal(1, game.RunTickCalls);
        Assert.Equal(ExplicitCycleResolutionPolicy.Administrator, game.LastResolutionRequest?.Policy);
    }

    [Fact]
    public async Task Admin_tick_endpoint_maps_authority_revocation_at_resolution_time()
    {
        var state = TestState.CreateSingleEmpireState();
        var admin = Assert.Single(state.Players);
        admin.Role = PlayerRole.Admin;
        var game = new FocusedAdminFixture(state)
        {
            ExplicitResult = new CycleResolutionResult.Forbidden()
        };

        var result = ApiAdminEndpoints.RunTick(
            CreateAuthenticatedContext(admin),
            game.GameId,
            game.Requests,
            game,
            TestState.Now);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Contains("\"code\":\"forbidden\"", response.Body, StringComparison.Ordinal);
        Assert.Equal(0, game.RunTickCalls);
        Assert.NotNull(game.LastResolutionRequest);
        Assert.True(game.LastResolutionRequest.RequireAdminister);
    }

    [Fact]
    public async Task Development_tick_endpoint_rejects_defeated_player()
    {
        var state = TestState.CreateSingleEmpireState();
        var player = Assert.Single(state.Players);
        MatchControl.DefeatEmpire(state, Assert.Single(state.Empires).EmpireId, TestState.Now);
        var game = new FocusedAdminFixture(state);

        var result = ApiAdminEndpoints.RunTick(
            CreateAuthenticatedContext(player),
            game.GameId,
            game.Requests,
            game,
            allowDevelopmentPlayer: true,
            TestState.Now);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(0, game.RunTickCalls);
    }

    [Fact]
    public async Task Admin_tick_endpoint_requires_login()
    {
        var state = TestState.CreateSingleEmpireState();
        var game = new FocusedAdminFixture(state);

        var result = ApiAdminEndpoints.RunTick(
            new DefaultHttpContext(),
            game.GameId,
            game.Requests,
            game,
            TestState.Now);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Contains("\"code\":\"authenticationRequired\"", response.Body, StringComparison.Ordinal);
        Assert.Equal(0, game.RunTickCalls);
    }

    private static DefaultHttpContext CreateAuthenticatedContext(Player player)
    {
        return TestHttpContextFactory.CreateAuthenticated(player);
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
            ApiJson.Configure(options.SerializerOptions);
        });
        return services.BuildServiceProvider();
    }

    private sealed class FocusedAdminFixture :
        IPlayerAccountQuery,
        IGameAccessQuery,
        IGameCommandAccessQuery,
        ICycleViewQuery,
        ICycleCommandStore,
        ICycleResolutionStore
    {
        private static readonly Guid SyntheticGameId = new("602a945d-b9dc-4c43-9db1-d87ece073743");
        private readonly GameState state;
        private readonly Cycle cycle;

        public FocusedAdminFixture(GameState state)
        {
            this.state = state;
            LegacyGameFoundation.Apply(state);
            cycle = state.GetActiveCycle()
                ?? throw new InvalidOperationException("The admin API test state requires one active Cycle.");
            GameId = cycle.GameId.GetValueOrDefault() is { } gameId && gameId != Guid.Empty
                ? gameId
                : SyntheticGameId;
            Requests = new SelectedGameRequestService(this, this, this, this, this);
        }

        public Guid GameId { get; }

        public SelectedGameRequestService Requests { get; }

        public int RunTickCalls { get; private set; }

        public ExplicitCycleResolutionRequest? LastResolutionRequest { get; private set; }

        public CycleResolutionResult? ExplicitResult { get; set; }

        public PlayerAccountSnapshot? Get(Guid playerId)
        {
            var player = state.Players.SingleOrDefault(item => item.PlayerId == playerId);
            return player is null
                ? null
                : new PlayerAccountSnapshot(
                    player.PlayerId,
                    player.Username,
                    player.Kind,
                    player.Role,
                    player.Status,
                    player.CreatedAt,
                    player.LastLoginAt);
        }

        public GameAccessSnapshot? Get(Guid playerId, Guid gameId)
        {
            if (gameId != GameId || state.Players.All(item => item.PlayerId != playerId))
            {
                return null;
            }

            var participant = state.MatchParticipants.SingleOrDefault(item =>
                item.CycleId == cycle.CycleId && item.PlayerId == playerId);
            var enrolment = state.GameEnrolments.SingleOrDefault(item =>
                item.GameId == GameId && item.PlayerId == playerId);
            return new GameAccessSnapshot(
                playerId,
                GameId,
                "Focused admin test Game",
                GamePurpose.Standard,
                GameLifecycleStatus.Active,
                GameVisibility.Private,
                cycle.CreatedByPlayerId,
                enrolment?.GameEnrolmentId,
                enrolment?.Status,
                cycle.CycleId,
                cycle.Status,
                cycle.CurrentTickNumber,
                cycle.TurnStage);
        }

        public GameCommandContext? Get(Guid playerId, GameCycleScope scope)
        {
            if (scope.GameId != GameId || scope.CycleId != cycle.CycleId)
            {
                return null;
            }

            var player = state.Players.SingleOrDefault(item => item.PlayerId == playerId);
            var participant = state.MatchParticipants.SingleOrDefault(item =>
                item.CycleId == cycle.CycleId && item.PlayerId == playerId);
            var enrolment = state.GameEnrolments.SingleOrDefault(item =>
                item.GameId == GameId && item.PlayerId == playerId);
            var empire = participant is null
                ? null
                : state.Empires.SingleOrDefault(item =>
                    item.CycleId == cycle.CycleId && item.EmpireId == participant.EmpireId);
            if (player is null || enrolment is null || participant is null || empire is null)
            {
                return null;
            }

            var permissions = GamePermission.Read;
            if (cycle.CreatedByPlayerId == playerId)
            {
                permissions |= GamePermission.Organise;
            }

            if (player.Role == PlayerRole.Admin)
            {
                permissions |= GamePermission.Administer;
            }

            return new GameCommandContext(
                new GameAccessContext(
                    playerId,
                    GameId,
                    enrolment.GameEnrolmentId,
                    permissions),
                cycle.CycleId,
                participant.MatchParticipantId,
                empire.EmpireId);
        }

        public ScopedQueryResult<T> Query<T>(
            GameCommandContext context,
            Func<GameState, T> projection) =>
            ContextMatches(context)
                ? new ScopedQueryResult<T>.Success(projection(state))
                : new ScopedQueryResult<T>.Unavailable();

        public ScopedCommandResult<T> Execute<T>(
            GameCommandContext context,
            Func<GameState, T> command) =>
            throw new NotSupportedException("Admin tick boundary tests do not issue Cycle commands.");

        public CycleResolutionResult ResolveIfDue(
            DueCycleWorkItem workItem,
            DateTimeOffset now) =>
            throw new NotSupportedException("Admin tick boundary tests resolve an explicit Cycle.");

        public CycleResolutionResult ResolveExplicit(
            ExplicitCycleResolutionRequest request,
            DateTimeOffset now)
        {
            LastResolutionRequest = request;
            if (ExplicitResult is not null)
            {
                return ExplicitResult;
            }

            if (!ContextMatches(request.Context))
            {
                return new CycleResolutionResult.Unavailable();
            }

            RunTickCalls++;
            var result = new TickEngine().RunTick(state, cycle.CycleId, now);
            return result.Status == TickLogStatus.Completed
                ? new CycleResolutionResult.Completed(result)
                : new CycleResolutionResult.RecoveryRequired(result);
        }

        private bool ContextMatches(GameCommandContext context) =>
            context.GameAccess.GameId == GameId
            && context.CycleId == cycle.CycleId
            && state.Players.Any(item => item.PlayerId == context.GameAccess.PlayerId)
            && state.GameEnrolments.Any(item =>
                item.GameEnrolmentId == context.GameAccess.GameEnrolmentId
                && item.GameId == context.GameAccess.GameId
                && item.PlayerId == context.GameAccess.PlayerId)
            && state.MatchParticipants.Any(item =>
                item.MatchParticipantId == context.MatchParticipantId
                && item.CycleId == context.CycleId
                && item.PlayerId == context.GameAccess.PlayerId
                && item.EmpireId == context.EmpireId)
            && state.Empires.Any(item =>
                item.EmpireId == context.EmpireId
                && item.CycleId == context.CycleId
                && item.PlayerId == context.GameAccess.PlayerId);
    }
}
