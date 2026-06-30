using Cycles.Core;

namespace Cycles.Tests;

public sealed class ApiVisibilityTests
{
    [Fact]
    public void Player_visibility_is_limited_to_systems_with_active_fleets()
    {
        var state = CreateVisibilityState();
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        var firstPlayer = state.Players.Single(player => player.Username == "first");
        var firstEmpire = state.Empires.Single(empire => empire.EmpireName == "First");
        var secondEmpire = state.Empires.Single(empire => empire.EmpireName == "Second");
        var contest = state.Systems.Single(system => system.SystemName == "Contest");
        var hidden = state.Systems.Single(system => system.SystemName == "Hidden");
        var actor = new DevelopmentActor(firstPlayer, firstEmpire);

        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);
        var visiblePresence = InfluenceCalculator.CalculateEffectivePresence(state, cycle.CycleId, contest.SystemId);
        var hiddenPresence = InfluenceCalculator.CalculateEffectivePresence(state, cycle.CycleId, hidden.SystemId);

        Assert.Contains(contest.SystemId, visibleSystemIds);
        Assert.DoesNotContain(hidden.SystemId, visibleSystemIds);
        Assert.Contains(secondEmpire.EmpireId, ApiVisibility.FilterPresence(actor, visibleSystemIds, contest.SystemId, visiblePresence).Keys);
        Assert.NotEmpty(hiddenPresence);
        Assert.Empty(ApiVisibility.FilterPresence(actor, visibleSystemIds, hidden.SystemId, hiddenPresence));
    }

    [Fact]
    public void Player_visibility_keeps_own_and_local_events_only()
    {
        var state = CreateVisibilityState();
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        var firstPlayer = state.Players.Single(player => player.Username == "first");
        var firstEmpire = state.Empires.Single(empire => empire.EmpireName == "First");
        var secondEmpire = state.Empires.Single(empire => empire.EmpireName == "Second");
        var contest = state.Systems.Single(system => system.SystemName == "Contest");
        var hidden = state.Systems.Single(system => system.SystemName == "Hidden");
        var actor = new DevelopmentActor(firstPlayer, firstEmpire);
        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);

        var ownEvent = CreateEvent(cycle.CycleId, empireId: firstEmpire.EmpireId);
        var localEvent = CreateEvent(cycle.CycleId, systemId: contest.SystemId);
        var hiddenEvent = CreateEvent(cycle.CycleId, systemId: hidden.SystemId);
        var enemyEvent = CreateEvent(cycle.CycleId, empireId: secondEmpire.EmpireId);

        Assert.True(ApiVisibility.CanSeeEvent(ownEvent, actor, visibleSystemIds));
        Assert.True(ApiVisibility.CanSeeEvent(localEvent, actor, visibleSystemIds));
        Assert.False(ApiVisibility.CanSeeEvent(hiddenEvent, actor, visibleSystemIds));
        Assert.False(ApiVisibility.CanSeeEvent(enemyEvent, actor, visibleSystemIds));
    }

    [Fact]
    public void Player_visibility_filters_chronicle_entries_by_visible_system()
    {
        var state = CreateVisibilityState();
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        var firstPlayer = state.Players.Single(player => player.Username == "first");
        var firstEmpire = state.Empires.Single(empire => empire.EmpireName == "First");
        var contest = state.Systems.Single(system => system.SystemName == "Contest");
        var hidden = state.Systems.Single(system => system.SystemName == "Hidden");
        var actor = new DevelopmentActor(firstPlayer, firstEmpire);
        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);

        Assert.True(ApiVisibility.CanSeeChronicleEntry(CreateChronicle(cycle.CycleId, contest.SystemId), actor, visibleSystemIds));
        Assert.True(ApiVisibility.CanSeeChronicleEntry(CreateChronicle(cycle.CycleId, systemId: null), actor, visibleSystemIds));
        Assert.False(ApiVisibility.CanSeeChronicleEntry(CreateChronicle(cycle.CycleId, hidden.SystemId), actor, visibleSystemIds));
    }

    [Fact]
    public void Admin_visibility_can_see_everything()
    {
        var state = CreateVisibilityState();
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        var admin = state.Players.Single(player => player.Username == "first");
        admin.Role = PlayerRole.Admin;
        var hidden = state.Systems.Single(system => system.SystemName == "Hidden");
        var actor = new DevelopmentActor(admin, null);
        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);
        var hiddenPresence = InfluenceCalculator.CalculateEffectivePresence(state, cycle.CycleId, hidden.SystemId);

        Assert.Contains(hidden.SystemId, visibleSystemIds);
        Assert.NotEmpty(ApiVisibility.FilterPresence(actor, visibleSystemIds, hidden.SystemId, hiddenPresence));
        Assert.True(ApiVisibility.CanSeeEvent(CreateEvent(cycle.CycleId, systemId: hidden.SystemId), actor, visibleSystemIds));
        Assert.True(ApiVisibility.CanSeeChronicleEntry(CreateChronicle(cycle.CycleId, hidden.SystemId), actor, visibleSystemIds));
    }

    private static GameState CreateVisibilityState()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 20, defenderShips: 20);
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        var secondEmpire = state.Empires.Single(empire => empire.EmpireName == "Second");
        var hidden = new GalaxySystem
        {
            CycleId = cycle.CycleId,
            SystemName = "Hidden",
            X = 200,
            Y = 200,
            IndustryOutput = 10,
            ResearchOutput = 10,
            PopulationOutput = 10,
            CreatedAt = TestState.Now
        };
        state.Systems.Add(hidden);
        state.Fleets.Add(new Fleet
        {
            CycleId = cycle.CycleId,
            EmpireId = secondEmpire.EmpireId,
            FleetName = "Hidden fleet",
            CurrentSystemId = hidden.SystemId,
            ShipCount = 30,
            Status = FleetStatus.Active,
            CreatedAt = TestState.Now
        });

        return state;
    }

    private static EventRecord CreateEvent(Guid cycleId, Guid? systemId = null, Guid? empireId = null) =>
        new()
        {
            CycleId = cycleId,
            TickNumber = 1,
            EventType = EventType.ResourcesGenerated,
            SystemId = systemId,
            EmpireId = empireId,
            Severity = EventSeverity.Low,
            DisplayText = "Event",
            FactJson = "{}",
            CreatedAt = TestState.Now
        };

    private static ChronicleEntry CreateChronicle(Guid cycleId, Guid? systemId) =>
        new()
        {
            CycleId = cycleId,
            SystemId = systemId,
            Title = "Chronicle",
            EntryType = ChronicleEntryType.Battle,
            ImportanceScore = 10,
            FactualSummary = "Chronicle",
            NarrativeText = "Chronicle",
            CreatedAt = TestState.Now
        };
}
