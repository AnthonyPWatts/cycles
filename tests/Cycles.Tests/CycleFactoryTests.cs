using Cycles.Core;

namespace Cycles.Tests;

public sealed class CycleFactoryTests
{
    private static readonly Guid GameId = Guid.Parse("02000000-0000-0000-0000-000000000001");
    private static readonly Guid ConfigurationId = Guid.Parse("02000000-0000-0000-0000-000000000002");
    private static readonly Guid[] PlayerIds =
    [
        Guid.Parse("02000000-0000-0000-0000-000000000011"),
        Guid.Parse("02000000-0000-0000-0000-000000000012"),
        Guid.Parse("02000000-0000-0000-0000-000000000013")
    ];

    [Fact]
    public void Standard_profile_materializes_existing_human_roster_without_creating_accounts()
    {
        var state = CreateStartingState(GameProfileCatalogue.Standard, humanCount: 2);
        var originalPlayerIds = state.Players.Select(item => item.PlayerId).Order().ToArray();

        var result = RosterAwareCycleFactory.Materialize(
            state,
            ConfigurationId,
            TestState.Now.AddMinutes(5));

        Assert.True(result.Created);
        Assert.Equal(GameProfileCatalogue.StandardProfileKey, result.ProfileKey);
        Assert.Equal(originalPlayerIds, state.Players.Select(item => item.PlayerId).Order().ToArray());
        Assert.Equal(2, state.MatchParticipants.Count);
        Assert.Equal(2, state.Empires.Count);
        Assert.Equal(2, state.Factions.Count(item => item.Kind == FactionKind.Empire));
        Assert.DoesNotContain(state.Players, item => item.Kind == PlayerKind.AI);
        Assert.Equal(2, state.Fleets.Count);
        Assert.All(state.MatchParticipants, item => Assert.Equal(GameId, item.GameId));
        Assert.All(state.Empires, empire => Assert.Contains(empire.PlayerId, originalPlayerIds));

        var cycle = Assert.Single(state.Cycles);
        Assert.Equal(ConfigurationId, cycle.CycleId);
        Assert.Equal(CycleSchedulingMode.Scheduled, cycle.SchedulingMode);
        Assert.Equal(TestState.Now.AddHours(1), cycle.NextTickAt);
        Assert.Equal(GameSeeder.CanonicalGalaxySectorCount, state.Sectors.Count);
        Assert.Equal(GameSeeder.CanonicalGalaxySystemCount, state.Systems.Count);
        Assert.Equal(GameSeeder.CanonicalGalaxyRouteCount, state.SystemLinks.Count);
        Assert.True(GameStateTransfer.Validate(state).IsValid);
    }

    [Fact]
    public void Twin_reaches_materializes_one_human_and_neutrals_as_scenario_actors()
    {
        var state = CreateStartingState(GameProfileCatalogue.TwinReaches, humanCount: 1);

        RosterAwareCycleFactory.Materialize(state, ConfigurationId, TestState.Now.AddMinutes(5));

        var cycle = Assert.Single(state.Cycles);
        Assert.Equal(CycleSchedulingMode.SelfPaced, cycle.SchedulingMode);
        Assert.Null(cycle.NextTickAt);
        Assert.Equal(2, state.Sectors.Count);
        Assert.Equal(10, state.Systems.Count);
        Assert.Equal(13, state.SystemLinks.Count);

        var participant = Assert.Single(state.MatchParticipants);
        var empire = Assert.Single(state.Empires);
        Assert.Equal(PlayerIds[0], participant.PlayerId);
        Assert.Equal("Wayfarer Compact", empire.EmpireName);
        Assert.Equal("Hearth", state.Systems.Single(item => item.SystemId == empire.HomeSystemId).SystemName);
        var resources = Assert.Single(state.EmpireResources);
        Assert.Equal(0, resources.Industry);
        Assert.Equal(80, resources.Research);
        Assert.Equal(100, resources.Population);

        var humanFleets = state.Fleets.Where(item => item.EmpireId == empire.EmpireId).ToArray();
        Assert.Equal(3, humanFleets.Length);
        Assert.Equal(
            [12, 20, 24],
            humanFleets.Select(item => item.ShipCount).Order().ToArray());
        var neutral = Assert.Single(state.Factions, item => item.Kind == FactionKind.Neutral);
        Assert.Equal("Drift Corsairs", neutral.FactionName);
        Assert.Equal(
            [4, 6],
            state.Fleets.Where(item => item.FactionId == neutral.FactionId)
                .Select(item => item.ShipCount)
                .Order()
                .ToArray());
        Assert.DoesNotContain(state.MatchParticipants, item =>
            state.Factions.Any(faction => faction.FactionId == neutral.FactionId
                                          && faction.EmpireId == item.EmpireId));
        Assert.Single(state.EmpireResources);
        Assert.True(GameStateTransfer.Validate(state).IsValid);
    }

    [Fact]
    public void Twin_reaches_first_move_uses_the_ordinary_order_and_tick_pipeline()
    {
        var state = CreateStartingState(GameProfileCatalogue.TwinReaches, humanCount: 1);
        var materializedAt = TestState.Now.AddMinutes(5);
        RosterAwareCycleFactory.Materialize(state, ConfigurationId, materializedAt);
        var cycle = Assert.Single(state.Cycles);
        var homeGuard = state.Fleets.Single(item => item.FleetName == "Home Guard");
        var firstlight = state.Systems.Single(item => item.SystemName == "Firstlight");

        var order = OrderService.SubmitMoveOrder(
            state,
            homeGuard.FleetId,
            firstlight.SystemId,
            materializedAt);
        var tick = new TickEngine().RunTick(state, cycle.CycleId, materializedAt.AddMinutes(1));

        Assert.Equal(TickLogStatus.Completed, tick.Status);
        Assert.Equal(FleetOrderStatus.Processed, state.FleetOrders.Single(item => item.FleetOrderId == order.FleetOrderId).Status);
        Assert.Equal(firstlight.SystemId, state.Fleets.Single(item => item.FleetId == homeGuard.FleetId).CurrentSystemId);
        Assert.Equal(1, state.Cycles.Single(item => item.CycleId == cycle.CycleId).CurrentTickNumber);
        Assert.True(GameStateTransfer.Validate(state).IsValid);
    }

    [Fact]
    public void Same_configuration_seed_and_roster_materialize_byte_for_byte_and_retry_is_idempotent()
    {
        var first = CreateStartingState(GameProfileCatalogue.TwinReaches, humanCount: 1);
        var second = CreateStartingState(GameProfileCatalogue.TwinReaches, humanCount: 1);
        var materializedAt = TestState.Now.AddMinutes(5);

        var firstResult = RosterAwareCycleFactory.Materialize(first, ConfigurationId, materializedAt);
        var secondResult = RosterAwareCycleFactory.Materialize(second, ConfigurationId, materializedAt);

        Assert.True(firstResult.Created);
        Assert.True(secondResult.Created);
        Assert.Equal(Write(first), Write(second));

        var beforeRetry = Write(first);
        var retry = RosterAwareCycleFactory.Materialize(
            first,
            ConfigurationId,
            materializedAt.AddHours(1));

        Assert.False(retry.Created);
        Assert.Equal(beforeRetry, Write(first));
        Assert.Single(first.Cycles);
        Assert.Single(first.Events, item => item.EventType == EventType.CycleSeeded);
        Assert.Single(first.GameLifecycleEvents, item => item.Type == GameLifecycleEventType.StatusChanged);
    }

    [Fact]
    public void Invalid_roster_fails_without_partial_materialization()
    {
        var state = CreateStartingState(GameProfileCatalogue.TwinReaches, humanCount: 2);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RosterAwareCycleFactory.Materialize(
                state,
                ConfigurationId,
                TestState.Now.AddMinutes(5)));

        Assert.Contains("requires 1–1", exception.Message, StringComparison.Ordinal);
        Assert.Empty(state.Cycles);
        Assert.Empty(state.Empires);
        Assert.Empty(state.Factions);
        Assert.Empty(state.Events);
        Assert.Equal(GameLifecycleStatus.Starting, Assert.Single(state.Games).Status);
        Assert.Equal(CycleConfigurationStatus.Locked, Assert.Single(state.CycleConfigurations).Status);
    }

    [Fact]
    public void Enrolled_ai_account_is_rejected_instead_of_becoming_a_human_seat()
    {
        var state = CreateStartingState(GameProfileCatalogue.TwinReaches, humanCount: 1);
        state.Players[0].Kind = PlayerKind.AI;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RosterAwareCycleFactory.Materialize(
                state,
                ConfigurationId,
                TestState.Now.AddMinutes(5)));

        Assert.Contains("active Human Player", exception.Message, StringComparison.Ordinal);
        Assert.Empty(state.Cycles);
        Assert.Single(state.Players);
    }

    private static GameState CreateStartingState(
        GameProfileDefinition profile,
        int humanCount)
    {
        var state = new GameState();
        for (var index = 0; index < humanCount; index++)
        {
            var player = new Player
            {
                PlayerId = PlayerIds[index],
                Username = $"Player {index + 1}",
                Kind = PlayerKind.Human,
                Role = index == 0 ? PlayerRole.Admin : PlayerRole.Player,
                Status = PlayerStatus.Active,
                CreatedAt = TestState.Now.AddDays(-1)
            };
            state.Players.Add(player);
            state.GameEnrolments.Add(new GameEnrolment
            {
                GameEnrolmentId = Guid.Parse($"02000000-0000-0000-0000-{(100 + index):D12}"),
                GameId = GameId,
                PlayerId = player.PlayerId,
                Status = GameEnrolmentStatus.Enrolled,
                Origin = GameEnrolmentOrigin.ManualOrganiser,
                EnrolledAt = TestState.Now.AddHours(-1),
                StatusChangedAt = TestState.Now.AddHours(-1)
            });
        }

        state.Games.Add(new Game
        {
            GameId = GameId,
            Name = profile.DisplayName,
            Purpose = profile.Purpose,
            Status = GameLifecycleStatus.Starting,
            Visibility = profile.GamePolicy.Visibility,
            CreationSource = profile.Purpose == GamePurpose.Training
                ? GameCreationSource.TrainingProvisioning
                : GameCreationSource.Operator,
            GamePolicyKey = profile.GamePolicy.Key,
            GamePolicyVersion = profile.GamePolicy.Version,
            GamePolicyContentHash = profile.GamePolicy.ContentHash,
            PolicyProvenanceStatus = ProvenanceStatus.Verified,
            CreatedByPlayerId = PlayerIds[0],
            CreatedAt = TestState.Now.AddHours(-1)
        });
        state.CycleConfigurations.Add(new CycleConfiguration
        {
            CycleConfigurationId = ConfigurationId,
            GameId = GameId,
            SequenceNumber = 1,
            Status = CycleConfigurationStatus.Locked,
            ProvenanceStatus = ProvenanceStatus.Verified,
            MapProfileKey = profile.Map.Key,
            MapProfileVersion = profile.Map.Version,
            MapProfileContentHash = profile.Map.ContentHash,
            MapSeed = 71421,
            ScenarioProfileKey = profile.Scenario.Key,
            ScenarioProfileVersion = profile.Scenario.Version,
            ScenarioProfileContentHash = profile.Scenario.ContentHash,
            ScenarioSeed = 20260720,
            CyclePolicyKey = profile.CyclePolicy.Key,
            CyclePolicyVersion = profile.CyclePolicy.Version,
            CyclePolicyContentHash = profile.CyclePolicy.ContentHash,
            SchedulingMode = profile.CyclePolicy.SchedulingMode,
            MinimumHumanSeats = profile.MinimumHumanSeats,
            MaximumHumanSeats = profile.MaximumHumanSeats,
            ScheduledStartAt = profile.CyclePolicy.SchedulingMode == CycleSchedulingMode.Scheduled
                ? TestState.Now.AddHours(1)
                : null,
            ScheduledEndAt = profile.CyclePolicy.SchedulingMode == CycleSchedulingMode.Scheduled
                ? TestState.Now.AddHours(1).AddDays(profile.CyclePolicy.DefaultDurationDays)
                : null,
            TickLengthMinutes = profile.CyclePolicy.TickLengthMinutes,
            CreatedAt = TestState.Now.AddHours(-1),
            LockedAt = TestState.Now
        });
        return state;
    }

    private static byte[] Write(GameState state)
    {
        using var stream = new MemoryStream();
        GameStateTransfer.Write(stream, state.DeepClone(), TestState.Now.AddDays(1));
        return stream.ToArray();
    }
}
