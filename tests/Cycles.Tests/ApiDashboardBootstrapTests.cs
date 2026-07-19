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
        var store = new CountingGameStateStore(state);

        var context = DashboardBootstrapContextFactory.Load(
            otherFleet.FleetId,
            TestHttpContextFactory.CreateAuthenticated(player),
            store);

        Assert.Equal(1, store.LoadCount);
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
        var store = new CountingGameStateStore(state);

        var context = DashboardBootstrapContextFactory.Load(
            selectedFleet.FleetId,
            TestHttpContextFactory.CreateAuthenticated(player),
            store);

        Assert.Equal(1, store.LoadCount);
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
        var store = new CountingGameStateStore(state);

        var context = DashboardBootstrapContextFactory.Load(
            selectedFleetId: null,
            TestHttpContextFactory.CreateAuthenticated(player),
            store);

        Assert.Contains(alliedFleet.CurrentSystemId, context.VisibleSystemIds);
        Assert.All(context.Fleets, fleet => Assert.Equal(empire.EmpireId, fleet.EmpireId));
        Assert.DoesNotContain(context.Fleets, fleet => fleet.FleetId == alliedFleet.FleetId);
        Assert.DoesNotContain(context.Orders, order => order.FleetOrderId == alliedOrder.FleetOrderId);
        Assert.DoesNotContain(context.Events, item => item.EventId == privateAlliedEvent.EventId);
    }

    private sealed class CountingGameStateStore(GameState state) : IGameStateStore
    {
        public string Description => "counting dashboard bootstrap store";
        public int LoadCount { get; private set; }

        public GameState LoadOrCreate()
        {
            LoadCount++;
            return state;
        }

        public T Update<T>(Func<GameState, T> update) => update(state);

        public TickResult RunTick(DateTimeOffset now) => throw new NotSupportedException();

        public TickResult? RunTickIfDue(DateTimeOffset now) => throw new NotSupportedException();

        public void Replace(GameState replacement) => state = replacement;
    }
}
