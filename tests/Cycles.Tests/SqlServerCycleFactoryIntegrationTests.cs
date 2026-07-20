using Cycles.Core;
using Cycles.Infrastructure.SqlServer;

namespace Cycles.Tests;

[Collection(SqlServerIntegrationCollection.CollectionName)]
[Trait(SqlIntegrationGuard.CategoryName, SqlIntegrationGuard.CategoryValue)]
public sealed class SqlServerCycleFactoryIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(GameProfileCatalogue.StandardProfileKey, 2)]
    [InlineData(GameProfileCatalogue.TwinReachesProfileKey, 1)]
    public void Materialized_profile_round_trips_through_the_existing_sql_contract(
        string profileKey,
        int humanCount)
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var profile = GameProfileCatalogue.All.Single(item => item.Key == profileKey);
        var state = CreateStartingState(profile, humanCount);
        var result = RosterAwareCycleFactory.Materialize(
            state,
            Assert.Single(state.CycleConfigurations).CycleConfigurationId,
            Now.AddMinutes(5));
        var store = new SqlServerGameStateStore(connectionString, () => new GameState());

        store.Replace(state);
        var loaded = store.LoadOrCreate();

        Assert.True(GameStateTransfer.Validate(loaded).IsValid);
        Assert.Equal(state.Players.Select(item => item.PlayerId).Order(),
            loaded.Players.Select(item => item.PlayerId).Order());
        Assert.Equal(humanCount, loaded.MatchParticipants.Count);
        var game = Assert.Single(loaded.Games);
        var configuration = Assert.Single(loaded.CycleConfigurations);
        var cycle = Assert.Single(loaded.Cycles);
        Assert.Equal(result.CycleId, cycle.CycleId);
        Assert.Equal(GameLifecycleStatus.Active, game.Status);
        Assert.Equal(CycleConfigurationStatus.Materialized, configuration.Status);
        Assert.Equal(profile.Map.ContentHash, cycle.MapProfileContentHash);
        Assert.Equal(profile.Scenario.ContentHash, cycle.ScenarioProfileContentHash);
        Assert.Equal(profile.CyclePolicy.ContentHash, cycle.CyclePolicyContentHash);
        Assert.NotEmpty(game.RowVersion);
        Assert.NotEmpty(configuration.RowVersion);
        Assert.All(loaded.GameEnrolments, item => Assert.NotEmpty(item.RowVersion));

        if (profile.Purpose == GamePurpose.Training)
        {
            var neutral = Assert.Single(loaded.Factions, item => item.Kind == FactionKind.Neutral);
            Assert.DoesNotContain(loaded.MatchParticipants, item => item.EmpireId == neutral.EmpireId);
            Assert.Equal(2, loaded.Fleets.Count(item => item.FactionId == neutral.FactionId));
        }
    }

    private static GameState CreateStartingState(GameProfileDefinition profile, int humanCount)
    {
        var gameId = Guid.Parse("03000000-0000-0000-0000-000000000001");
        var configurationId = Guid.Parse("03000000-0000-0000-0000-000000000002");
        var state = new GameState();
        for (var index = 0; index < humanCount; index++)
        {
            var playerId = Guid.Parse($"03000000-0000-0000-0000-{(100 + index):D12}");
            state.Players.Add(new Player
            {
                PlayerId = playerId,
                Username = $"SQL Player {index + 1}",
                Kind = PlayerKind.Human,
                Status = PlayerStatus.Active,
                CreatedAt = Now.AddDays(-1)
            });
            state.GameEnrolments.Add(new GameEnrolment
            {
                GameEnrolmentId = Guid.Parse($"03000000-0000-0000-0000-{(200 + index):D12}"),
                GameId = gameId,
                PlayerId = playerId,
                Status = GameEnrolmentStatus.Enrolled,
                Origin = GameEnrolmentOrigin.ManualOrganiser,
                EnrolledAt = Now.AddHours(-1),
                StatusChangedAt = Now.AddHours(-1)
            });
        }

        state.Games.Add(new Game
        {
            GameId = gameId,
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
            CreatedByPlayerId = state.Players[0].PlayerId,
            CreatedAt = Now.AddHours(-1)
        });
        state.CycleConfigurations.Add(new CycleConfiguration
        {
            CycleConfigurationId = configurationId,
            GameId = gameId,
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
                ? Now.AddHours(1)
                : null,
            ScheduledEndAt = profile.CyclePolicy.SchedulingMode == CycleSchedulingMode.Scheduled
                ? Now.AddHours(1).AddDays(profile.CyclePolicy.DefaultDurationDays)
                : null,
            TickLengthMinutes = profile.CyclePolicy.TickLengthMinutes,
            CreatedAt = Now.AddHours(-1),
            LockedAt = Now
        });
        return state;
    }
}
