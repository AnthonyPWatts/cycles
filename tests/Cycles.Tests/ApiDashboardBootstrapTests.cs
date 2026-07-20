using Cycles.Application;
using Cycles.Core;

namespace Cycles.Tests;

public sealed class ApiDashboardBootstrapTests
{
    [Fact]
    public void Bootstrap_context_uses_one_snapshot_and_does_not_honour_another_empires_selection()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var cycle = state.GetActiveCycle()!;
        var player = state.Players.Single(item => item.Username == "Tony");
        var empire = state.Empires.Single(item => item.PlayerId == player.PlayerId);
        var otherFleet = state.Fleets.First(item => item.EmpireId != Guid.Empty && item.EmpireId != empire.EmpireId);
        var game = new FocusedDashboardFixture(state);

        var context = DashboardBootstrapContextFactory.Load(
            otherFleet.FleetId,
            TestHttpContextFactory.CreateAuthenticated(player),
            game.GameId,
            game.Requests);

        Assert.Equal(1, game.QueryCount);
        Assert.Equal(empire.EmpireId, context.Empire.EmpireId);
        Assert.All(context.Fleets, fleet => Assert.Equal(empire.EmpireId, fleet.EmpireId));
        Assert.DoesNotContain(context.Fleets, fleet => fleet.FleetId == otherFleet.FleetId);
        Assert.NotNull(context.SelectedFleet);
        Assert.Equal(empire.EmpireId, context.SelectedFleet.EmpireId);
        Assert.True(context.VisibleSystemIds.SetEquals(
            ApiVisibility.GetVisibleSystemIds(state, cycle, context.Actor)));
        var actorFleetIds = context.Fleets.Select(fleet => fleet.FleetId).ToHashSet();
        Assert.All(context.Orders, order => Assert.Contains(order.FleetId, actorFleetIds));
        Assert.All(
            context.Events,
            item => Assert.True(ApiVisibility.CanSeeEvent(item, context.Actor, context.VisibleSystemIds)));
        Assert.All(
            context.ChronicleEntries,
            entry => Assert.True(ApiVisibility.CanSeeChronicleEntry(entry, context.Actor, context.VisibleSystemIds)));
    }

    [Fact]
    public void Bootstrap_context_preserves_an_owned_selected_fleet()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var player = state.Players.Single(item => item.Username == "Tony");
        var empire = state.Empires.Single(item => item.PlayerId == player.PlayerId);
        var selectedFleet = state.Fleets.Last(item => item.EmpireId == empire.EmpireId);
        var game = new FocusedDashboardFixture(state);

        var context = DashboardBootstrapContextFactory.Load(
            selectedFleet.FleetId,
            TestHttpContextFactory.CreateAuthenticated(player),
            game.GameId,
            game.Requests);

        Assert.Equal(1, game.QueryCount);
        Assert.Equal(selectedFleet.FleetId, context.SelectedFleet?.FleetId);
    }

    [Fact]
    public void Bootstrap_context_adds_allied_visibility_without_pooling_private_empire_state()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var cycle = state.GetActiveCycle()!;
        var player = state.Players.Single(item => item.Username == "Tony");
        var empire = state.Empires.Single(item => item.PlayerId == player.PlayerId);
        var alliedEmpire = state.Empires.Single(item => item.PlayerId == state.Players.Single(playerItem => playerItem.Username == "Will").PlayerId);
        var actor = new DevelopmentActor(player, empire);
        var visibleBeforeAlliance = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);
        var alliedFleet = state.Fleets.First(item => item.EmpireId == alliedEmpire.EmpireId
                                                    && item.Status == FleetStatus.Active
                                                    && item.ShipCount > 0
                                                    && !visibleBeforeAlliance.Contains(item.CurrentSystemId));
        var alliedOrder = OrderService.SubmitHoldOrder(state, alliedFleet.FleetId, TestState.Now);
        var privateAlliedEvent = new EventRecord
        {
            CycleId = cycle.CycleId,
            TickNumber = 0,
            EventType = EventType.ResourcesGenerated,
            EmpireId = alliedEmpire.EmpireId,
            Severity = EventSeverity.Low,
            DisplayText = "Private allied event",
            FactJson = "{}",
            CreatedAt = TestState.Now
        };
        state.Events.Add(privateAlliedEvent);
        DiplomacyService.SetState(
            state,
            cycle.CycleId,
            empire.EmpireId,
            alliedEmpire.EmpireId,
            DiplomaticRelationshipState.Alliance,
            tickNumber: 0,
            TestState.Now);
        var game = new FocusedDashboardFixture(state);

        var context = DashboardBootstrapContextFactory.Load(
            selectedFleetId: null,
            TestHttpContextFactory.CreateAuthenticated(player),
            game.GameId,
            game.Requests);

        Assert.Contains(alliedFleet.CurrentSystemId, context.VisibleSystemIds);
        Assert.All(context.Fleets, fleet => Assert.Equal(empire.EmpireId, fleet.EmpireId));
        Assert.DoesNotContain(context.Fleets, fleet => fleet.FleetId == alliedFleet.FleetId);
        Assert.DoesNotContain(context.Orders, order => order.FleetOrderId == alliedOrder.FleetOrderId);
        Assert.DoesNotContain(context.Events, item => item.EventId == privateAlliedEvent.EventId);
    }

    [Fact]
    public void Bootstrap_context_keeps_the_complete_latest_visible_tick_with_a_bounded_recent_remainder()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var cycle = state.GetActiveCycle()!;
        var player = state.Players.Single(item => item.Username == "Tony");
        var empire = state.Empires.Single(item => item.PlayerId == player.PlayerId);
        var latestEventIds = new List<Guid>();
        for (var index = 0; index < 30; index++)
        {
            var item = VisibleEmpireEvent(cycle.CycleId, empire.EmpireId, tickNumber: 7, index: index);
            latestEventIds.Add(item.EventId);
            state.Events.Add(item);
        }

        for (var index = 0; index < 100; index++)
        {
            state.Events.Add(VisibleEmpireEvent(cycle.CycleId, empire.EmpireId, tickNumber: 6, index: index));
        }

        var foreignEvent = VisibleEmpireEvent(Guid.NewGuid(), empire.EmpireId, tickNumber: 99, index: 0);
        state.Events.Add(foreignEvent);
        var game = new FocusedDashboardFixture(state);
        var context = DashboardBootstrapContextFactory.Load(
            selectedFleetId: null,
            TestHttpContextFactory.CreateAuthenticated(player),
            game.GameId,
            game.Requests);

        Assert.Equal(100, context.Events.Count);
        Assert.All(latestEventIds, eventId => Assert.Contains(context.Events, item => item.EventId == eventId));
        Assert.DoesNotContain(context.Events, item => item.EventId == foreignEvent.EventId);
        Assert.All(context.Events, item => Assert.Equal(cycle.CycleId, item.CycleId));
    }

    [Fact]
    public void Bootstrap_context_does_not_cut_a_latest_visible_tick_that_exceeds_the_normal_limit()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var cycle = state.GetActiveCycle()!;
        var player = state.Players.Single(item => item.Username == "Tony");
        var empire = state.Empires.Single(item => item.PlayerId == player.PlayerId);
        var latestEventIds = Enumerable.Range(0, 110)
            .Select(index => VisibleEmpireEvent(cycle.CycleId, empire.EmpireId, tickNumber: 8, index: index))
            .Select(item =>
            {
                state.Events.Add(item);
                return item.EventId;
            })
            .ToArray();
        var game = new FocusedDashboardFixture(state);

        var context = DashboardBootstrapContextFactory.Load(
            selectedFleetId: null,
            TestHttpContextFactory.CreateAuthenticated(player),
            game.GameId,
            game.Requests);

        Assert.Equal(110, context.Events.Count);
        Assert.All(latestEventIds, eventId => Assert.Contains(context.Events, item => item.EventId == eventId));
        Assert.All(context.Events, item => Assert.Equal(8, item.TickNumber));
    }

    private static EventRecord VisibleEmpireEvent(
        Guid cycleId,
        Guid empireId,
        int tickNumber,
        int index) =>
        new()
        {
            CycleId = cycleId,
            TickNumber = tickNumber,
            EventType = EventType.ResourcesGenerated,
            EmpireId = empireId,
            Severity = EventSeverity.Low,
            DisplayText = $"Visible event {tickNumber}-{index}",
            FactJson = "{}",
            CreatedAt = TestState.Now.AddMinutes(index)
        };

    private sealed class FocusedDashboardFixture :
        IPlayerAccountQuery,
        IGameAccessQuery,
        IGameCommandAccessQuery,
        ICycleViewQuery,
        ICycleCommandStore
    {
        private static readonly Guid SyntheticGameId = new("225e716e-08fa-43c9-8424-936563535f07");
        private readonly GameState state;
        private readonly Cycle cycle;

        public FocusedDashboardFixture(GameState state)
        {
            this.state = state;
            cycle = state.GetActiveCycle()
                ?? throw new InvalidOperationException("The dashboard API test state requires one active Cycle.");
            GameId = cycle.GameId.GetValueOrDefault() is { } gameId && gameId != Guid.Empty
                ? gameId
                : SyntheticGameId;
            Requests = new SelectedGameRequestService(this, this, this, this, this);
        }

        public Guid GameId { get; }

        public SelectedGameRequestService Requests { get; }

        public int QueryCount { get; private set; }

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
            return new GameAccessSnapshot(
                playerId,
                GameId,
                "Focused dashboard test Game",
                GamePurpose.Standard,
                GameLifecycleStatus.Active,
                GameVisibility.Private,
                cycle.CreatedByPlayerId,
                participant?.MatchParticipantId,
                participant is null ? null : GameEnrolmentStatus.Enrolled,
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
            var empire = participant is null
                ? null
                : state.Empires.SingleOrDefault(item =>
                    item.CycleId == cycle.CycleId && item.EmpireId == participant.EmpireId);
            if (player is null || participant is null || empire is null)
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
                    participant.MatchParticipantId,
                    permissions),
                cycle.CycleId,
                participant.MatchParticipantId,
                empire.EmpireId);
        }

        public ScopedQueryResult<T> Query<T>(
            GameCommandContext context,
            Func<GameState, T> projection)
        {
            QueryCount++;
            return ContextMatches(context)
                ? new ScopedQueryResult<T>.Success(projection(state))
                : new ScopedQueryResult<T>.Unavailable();
        }

        public ScopedCommandResult<T> Execute<T>(
            GameCommandContext context,
            Func<GameState, T> command) =>
            ContextMatches(context)
                ? new ScopedCommandResult<T>.Success(command(state))
                : new ScopedCommandResult<T>.Unavailable();

        private bool ContextMatches(GameCommandContext context) =>
            context.GameAccess.GameId == GameId
            && context.CycleId == cycle.CycleId
            && state.Players.Any(item => item.PlayerId == context.GameAccess.PlayerId)
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
