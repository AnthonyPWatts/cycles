using Cycles.Application;
using Cycles.Core;

namespace Cycles.Tests;

public sealed class SelectedGameRequestServiceTests
{
    [Fact]
    public void Unknown_and_withdrawn_games_are_equally_unavailable()
    {
        var fixture = new FocusedStoreFixture();
        var service = fixture.CreateService();
        var request = TestHttpContextFactory.CreateAuthenticated(fixture.Player);

        fixture.Access = null;
        var unknown = Assert.Throws<ApiNotFoundException>(() =>
            service.RequireContext(request, fixture.GameId));

        fixture.Access = fixture.CreateAccess(GameEnrolmentStatus.Withdrawn);
        var withdrawn = Assert.Throws<ApiNotFoundException>(() =>
            service.RequireContext(request, fixture.GameId));

        Assert.Equal(unknown.Message, withdrawn.Message);
        Assert.Equal("Game is unavailable.", unknown.Message);
    }

    [Fact]
    public void Enrolled_game_without_a_playable_current_cycle_returns_a_typed_conflict()
    {
        var fixture = new FocusedStoreFixture
        {
            CommandContext = null
        };
        var service = fixture.CreateService();

        var error = Assert.Throws<ApiStateConflictException>(() => service.RequireContext(
            TestHttpContextFactory.CreateAuthenticated(fixture.Player),
            fixture.GameId));

        Assert.Contains("not currently playable", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Focused_view_unavailable_is_non_disclosing()
    {
        var fixture = new FocusedStoreFixture
        {
            QueryUnavailable = true
        };
        var service = fixture.CreateService();

        var error = Assert.Throws<ApiNotFoundException>(() => service.Query(
            TestHttpContextFactory.CreateAuthenticated(fixture.Player),
            fixture.GameId,
            static (_, context) => context.CycleId));

        Assert.Equal("Game is unavailable.", error.Message);
    }

    [Fact]
    public void Focused_command_busy_and_stale_results_fail_without_running_twice()
    {
        var fixture = new FocusedStoreFixture
        {
            CommandResult = FocusedCommandResult.Busy
        };
        var service = fixture.CreateService();
        var request = TestHttpContextFactory.CreateAuthenticated(fixture.Player);
        var callbackCalls = 0;

        var busy = Assert.Throws<ApiStateConflictException>(() => service.Command(
            request,
            fixture.GameId,
            (_, _) => ++callbackCalls));

        fixture.CommandResult = FocusedCommandResult.Unavailable;
        var unavailable = Assert.Throws<ApiForbiddenException>(() => service.Command(
            request,
            fixture.GameId,
            (_, _) => ++callbackCalls));

        Assert.Contains("temporarily busy", busy.Message, StringComparison.Ordinal);
        Assert.Contains("no longer accepts commands", unavailable.Message, StringComparison.Ordinal);
        Assert.Equal(0, callbackCalls);
    }

    [Fact]
    public void Success_binds_the_projection_to_the_authoritative_game_and_cycle_context()
    {
        var fixture = new FocusedStoreFixture();
        var service = fixture.CreateService();

        var scope = service.Query(
            TestHttpContextFactory.CreateAuthenticated(fixture.Player),
            fixture.GameId,
            static (_, context) => context.Scope);

        Assert.Equal(fixture.GameId, scope.GameId);
        Assert.Equal(fixture.CycleId, scope.CycleId);
        Assert.Equal(1, fixture.QueryCalls);
    }

    private enum FocusedCommandResult
    {
        Success,
        Unavailable,
        Busy
    }

    private sealed class FocusedStoreFixture :
        IPlayerAccountQuery,
        IGameAccessQuery,
        IGameCommandAccessQuery,
        ICycleViewQuery,
        ICycleCommandStore
    {
        public FocusedStoreFixture()
        {
            Player = new Player
            {
                PlayerId = Guid.NewGuid(),
                Username = "scoped-player",
                Kind = PlayerKind.Human,
                Status = PlayerStatus.Active,
                CreatedAt = TestState.Now
            };
            GameId = Guid.NewGuid();
            CycleId = Guid.NewGuid();
            EnrolmentId = Guid.NewGuid();
            MatchParticipantId = Guid.NewGuid();
            EmpireId = Guid.NewGuid();
            Access = CreateAccess(GameEnrolmentStatus.Enrolled);
            CommandContext = new GameCommandContext(
                new GameAccessContext(Player.PlayerId, GameId, EnrolmentId, GamePermission.Read),
                CycleId,
                MatchParticipantId,
                EmpireId);
        }

        public Player Player { get; }

        public Guid GameId { get; }

        public Guid CycleId { get; }

        public Guid EnrolmentId { get; }

        public Guid MatchParticipantId { get; }

        public Guid EmpireId { get; }

        public GameAccessSnapshot? Access { get; set; }

        public GameCommandContext? CommandContext { get; set; }

        public bool QueryUnavailable { get; set; }

        public FocusedCommandResult CommandResult { get; set; }

        public int QueryCalls { get; private set; }

        public SelectedGameRequestService CreateService() => new(this, this, this, this, this);

        public GameAccessSnapshot CreateAccess(GameEnrolmentStatus status) => new(
            Player.PlayerId,
            GameId,
            "Scoped Game",
            GamePurpose.Standard,
            GameLifecycleStatus.Active,
            GameVisibility.Private,
            Player.PlayerId,
            EnrolmentId,
            status,
            CycleId,
            CycleStatus.Active,
            0,
            TurnResolutionStage.CommandOpen);

        PlayerAccountSnapshot? IPlayerAccountQuery.Get(Guid playerId) =>
            playerId == Player.PlayerId
                ? new PlayerAccountSnapshot(
                    Player.PlayerId,
                    Player.Username,
                    Player.Kind,
                    Player.Role,
                    Player.Status,
                    Player.CreatedAt,
                    Player.LastLoginAt)
                : null;

        GameAccessSnapshot? IGameAccessQuery.Get(Guid playerId, Guid gameId) =>
            playerId == Player.PlayerId && gameId == GameId ? Access : null;

        GameCommandContext? IGameCommandAccessQuery.Get(Guid playerId, GameCycleScope scope) =>
            playerId == Player.PlayerId && scope.GameId == GameId && scope.CycleId == CycleId
                ? CommandContext
                : null;

        ScopedQueryResult<T> ICycleViewQuery.Query<T>(
            GameCommandContext context,
            Func<GameState, T> projection)
        {
            QueryCalls++;
            return QueryUnavailable
                ? new ScopedQueryResult<T>.Unavailable()
                : new ScopedQueryResult<T>.Success(projection(new GameState()));
        }

        ScopedCommandResult<T> ICycleCommandStore.Execute<T>(
            GameCommandContext context,
            Func<GameState, T> command) =>
            CommandResult switch
            {
                FocusedCommandResult.Success => new ScopedCommandResult<T>.Success(command(new GameState())),
                FocusedCommandResult.Unavailable => new ScopedCommandResult<T>.Unavailable(),
                FocusedCommandResult.Busy => new ScopedCommandResult<T>.Busy(),
                _ => throw new ArgumentOutOfRangeException()
            };
    }
}
