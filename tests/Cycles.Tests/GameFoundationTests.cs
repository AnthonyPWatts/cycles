using Cycles.Core;

namespace Cycles.Tests;

public sealed class GameFoundationTests
{
    [Fact]
    public void Legacy_adaptation_derives_one_deterministic_standard_game()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var cycle = Assert.Single(state.Cycles);

        LegacyGameFoundation.Apply(state);

        var game = Assert.Single(state.Games);
        Assert.Equal(GameFoundationConstants.LegacyGameId, game.GameId);
        Assert.Equal(GameFoundationConstants.LegacyGameName, game.Name);
        Assert.Equal(GamePurpose.Standard, game.Purpose);
        Assert.Equal(GameLifecycleStatus.Active, game.Status);
        Assert.Equal(GameVisibility.Private, game.Visibility);
        Assert.Equal(GameCreationSource.LegacyImport, game.CreationSource);
        Assert.Equal(GameFoundationConstants.LegacyGamePolicyKey, game.GamePolicyKey);
        Assert.Equal(GameFoundationConstants.LegacyGamePolicyVersion, game.GamePolicyVersion);
        Assert.Equal(ProvenanceStatus.LegacyUnverified, game.PolicyProvenanceStatus);
        Assert.Equal(TestState.Now, game.CreatedAt);
        Assert.Equal(TestState.Now, game.FirstStartedAt);
        Assert.Null(game.CompletedAt);

        var configuration = Assert.Single(state.CycleConfigurations);
        Assert.Equal(cycle.CycleId, configuration.CycleConfigurationId);
        Assert.Equal(GameFoundationConstants.LegacyGameId, configuration.GameId);
        Assert.Equal(1, configuration.SequenceNumber);
        Assert.Equal(CycleConfigurationStatus.Materialized, configuration.Status);
        Assert.Equal(ProvenanceStatus.LegacyUnverified, configuration.ProvenanceStatus);
        Assert.Equal(GameSeeder.CanonicalGalaxyTopologyKey, configuration.MapProfileKey);
        Assert.Null(configuration.MapProfileVersion);
        Assert.Null(configuration.MapProfileContentHash);
        Assert.Equal(71421, configuration.MapSeed);
        Assert.Equal(GameSeeder.CuratedColdStartScenarioKey, configuration.ScenarioProfileKey);
        Assert.Null(configuration.ScenarioProfileVersion);
        Assert.Null(configuration.ScenarioProfileContentHash);
        Assert.Equal(GameSeeder.DefaultDevelopmentScenarioSeed, configuration.ScenarioSeed);
        Assert.Equal(GameFoundationConstants.LegacyCyclePolicyKey, configuration.CyclePolicyKey);
        Assert.Equal(TestState.Now, configuration.LockedAt);
        Assert.Equal(TestState.Now, configuration.MaterializedAt);

        Assert.Equal(GameFoundationConstants.LegacyGameId, cycle.GameId);
        Assert.Equal(cycle.CycleId, cycle.CycleConfigurationId);
        Assert.Null(cycle.PreviousCycleId);
        Assert.Equal(configuration.MapProfileKey, cycle.MapProfileKey);
        Assert.Equal(configuration.MapSeed, cycle.MapSeed);
        Assert.Equal(configuration.ScenarioProfileKey, cycle.ScenarioProfileKey);
        Assert.Equal(configuration.ScenarioSeed, cycle.ScenarioSeed);
        Assert.Equal(ProvenanceStatus.LegacyUnverified, cycle.ProfileProvenanceStatus);

        Assert.Equal(state.MatchParticipants.Select(item => item.PlayerId).Distinct().Count(), state.GameEnrolments.Count);
        Assert.All(state.GameEnrolments, enrolment =>
        {
            Assert.Equal(enrolment.PlayerId, enrolment.GameEnrolmentId);
            Assert.Equal(GameFoundationConstants.LegacyGameId, enrolment.GameId);
            Assert.Equal(GameEnrolmentStatus.Enrolled, enrolment.Status);
            Assert.Equal(GameEnrolmentOrigin.LegacyImport, enrolment.Origin);
            Assert.Null(enrolment.EndedAt);
        });

        var importEvent = Assert.Single(state.GameLifecycleEvents);
        Assert.Equal(GameFoundationConstants.LegacyLifecycleEventId, importEvent.GameLifecycleEventId);
        Assert.Equal(GameLifecycleEventType.LegacyImported, importEvent.Type);
        Assert.Equal(GameFoundationConstants.LegacyImportFactJson, importEvent.FactJson);
    }

    [Fact]
    public void Legacy_adaptation_updates_lifecycle_and_adds_a_successor_configuration_idempotently()
    {
        var state = TestState.CreateSingleEmpireState();
        var firstCycle = Assert.Single(state.Cycles);
        var firstParticipant = Assert.Single(state.MatchParticipants);
        LegacyGameFoundation.Apply(state);

        firstCycle.Status = CycleStatus.Completed;
        firstParticipant.Status = MatchParticipantStatus.Completed;
        firstParticipant.EndedAt = firstCycle.EndAt;
        LegacyGameFoundation.Apply(state);

        Assert.Equal(GameLifecycleStatus.Completed, Assert.Single(state.Games).Status);
        Assert.Equal(firstCycle.EndAt, Assert.Single(state.Games).CompletedAt);
        Assert.Equal(GameEnrolmentStatus.Completed, Assert.Single(state.GameEnrolments).Status);

        var successor = new Cycle
        {
            Name = "Successor",
            StartAt = firstCycle.EndAt.AddDays(1),
            EndAt = firstCycle.EndAt.AddDays(91),
            TickLengthMinutes = firstCycle.TickLengthMinutes,
            Status = CycleStatus.Active,
            CreatedAt = firstCycle.EndAt.AddHours(1)
        };
        state.Cycles.Add(successor);
        state.MatchParticipants.Add(new MatchParticipant
        {
            CycleId = successor.CycleId,
            PlayerId = firstParticipant.PlayerId,
            EmpireId = firstParticipant.EmpireId,
            Status = MatchParticipantStatus.Active,
            JoinedAt = successor.CreatedAt
        });

        LegacyGameFoundation.Apply(state);
        LegacyGameFoundation.Apply(state);

        var game = Assert.Single(state.Games);
        Assert.Equal(GameLifecycleStatus.Active, game.Status);
        Assert.Null(game.CompletedAt);
        Assert.Equal(2, state.CycleConfigurations.Count);
        var successorConfiguration = Assert.Single(
            state.CycleConfigurations,
            item => item.CycleConfigurationId == successor.CycleId);
        Assert.Equal(2, successorConfiguration.SequenceNumber);
        Assert.Equal(GameFoundationConstants.LegacyUnclassifiedProfileKey, successorConfiguration.MapProfileKey);
        Assert.Equal(GameFoundationConstants.LegacyUnclassifiedProfileKey, successorConfiguration.ScenarioProfileKey);
        Assert.Equal(GameFoundationConstants.LegacyGameId, successor.GameId);
        Assert.Equal(successor.CycleId, successor.CycleConfigurationId);
        Assert.Null(successor.PreviousCycleId);
        Assert.Equal(GameEnrolmentStatus.Enrolled, Assert.Single(state.GameEnrolments).Status);
        Assert.Single(state.GameLifecycleEvents);
    }

    [Fact]
    public void Legacy_adaptation_rejects_ambiguous_operational_lineage()
    {
        var state = new GameState
        {
            Cycles =
            [
                new Cycle { Name = "First", Status = CycleStatus.Active, StartAt = TestState.Now, CreatedAt = TestState.Now },
                new Cycle { Name = "Second", Status = CycleStatus.RecoveryRequired, StartAt = TestState.Now, CreatedAt = TestState.Now }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => LegacyGameFoundation.Apply(state));

        Assert.Contains("at most one operational Cycle", exception.Message, StringComparison.Ordinal);
        Assert.Empty(state.Games);
    }

    [Fact]
    public void Deep_clone_copies_game_foundations_and_row_versions()
    {
        var state = TestState.CreateSingleEmpireState();
        LegacyGameFoundation.Apply(state);
        state.Games[0].RowVersion = [1, 2, 3];
        state.CycleConfigurations[0].RowVersion = [4, 5, 6];
        state.GameEnrolments[0].RowVersion = [7, 8, 9];
        state.GameLifecycleEvents[0].Reason = "Imported for compatibility.";

        var clone = state.DeepClone();

        Assert.NotSame(state.Games[0], clone.Games[0]);
        Assert.NotSame(state.CycleConfigurations[0], clone.CycleConfigurations[0]);
        Assert.NotSame(state.GameEnrolments[0], clone.GameEnrolments[0]);
        Assert.NotSame(state.GameLifecycleEvents[0], clone.GameLifecycleEvents[0]);
        Assert.NotSame(state.Games[0].RowVersion, clone.Games[0].RowVersion);
        Assert.NotSame(state.CycleConfigurations[0].RowVersion, clone.CycleConfigurations[0].RowVersion);
        Assert.NotSame(state.GameEnrolments[0].RowVersion, clone.GameEnrolments[0].RowVersion);
        Assert.Equal(state.Cycles[0].GameId, clone.Cycles[0].GameId);
        Assert.Equal(state.Cycles[0].CycleConfigurationId, clone.Cycles[0].CycleConfigurationId);
        Assert.Equal(state.Cycles[0].MapProfileKey, clone.Cycles[0].MapProfileKey);

        clone.Games[0].RowVersion[0] = 99;
        clone.CycleConfigurations[0].RowVersion[0] = 99;
        clone.GameEnrolments[0].RowVersion[0] = 99;
        clone.GameLifecycleEvents[0].Reason = "Changed.";

        Assert.Equal(1, state.Games[0].RowVersion[0]);
        Assert.Equal(4, state.CycleConfigurations[0].RowVersion[0]);
        Assert.Equal(7, state.GameEnrolments[0].RowVersion[0]);
        Assert.Equal("Imported for compatibility.", state.GameLifecycleEvents[0].Reason);
    }
}
