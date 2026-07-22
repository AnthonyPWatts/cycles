using System.Text.Json;
using Cycles.Core;

namespace Cycles.Tests;

public sealed class CycleEndTests
{
    [Fact]
    public void CompleteCyclePersistsFinalRankingsAndMarksCycleCompleted()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 20);
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        cycle.CurrentTickNumber = 12;
        cycle.NextTickAt = TestState.Now.AddMinutes(30);
        var firstEmpire = state.Empires.Single(empire => empire.EmpireName == "First");
        var secondEmpire = state.Empires.Single(empire => empire.EmpireName == "Second");
        LegacyGameFoundation.Apply(state);

        Assert.Equal(GameLifecycleStatus.Active, Assert.Single(state.Games).Status);

        var rankings = CycleEndService.CompleteCycle(state, cycle.CycleId, TestState.Now);

        Assert.Equal(CycleStatus.Completed, cycle.Status);
        Assert.Null(cycle.NextTickAt);
        Assert.Equal(2, rankings.Count);
        Assert.Equal(2, state.CycleRankings.Count);

        var winner = rankings.Single(ranking => ranking.IsWinner);
        var runnerUp = rankings.Single(ranking => !ranking.IsWinner);
        Assert.Equal(firstEmpire.EmpireId, winner.EmpireId);
        Assert.Equal(secondEmpire.EmpireId, runnerUp.EmpireId);
        Assert.Equal(1, winner.Rank);
        Assert.Equal(2, runnerUp.Rank);
        Assert.Equal(80m, winner.MapControlPercent);
        Assert.Equal(20m, runnerUp.MapControlPercent);
        Assert.Equal(12, winner.CutoffTickNumber);
        Assert.Equal(TestState.Now, winner.CutoffAt);

        var completionEvent = Assert.Single(state.Events, item => item.EventType == EventType.CycleCompleted);
        Assert.Equal(EventSeverity.Historic, completionEvent.Severity);
        Assert.Equal(winner.EmpireId, completionEvent.EmpireId);
        var game = Assert.Single(state.Games);
        Assert.Equal(GameLifecycleStatus.Completed, game.Status);
        Assert.Equal(TestState.Now, game.CompletedAt);
        Assert.All(state.GameEnrolments, enrolment =>
        {
            Assert.Equal(GameEnrolmentStatus.Completed, enrolment.Status);
            Assert.Equal(TestState.Now, enrolment.EndedAt);
        });
    }

    [Fact]
    public void CompleteCyclePreservesAuthoritativeFoundationMetadataAndNullableSchedule()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 20);
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        LegacyGameFoundation.Apply(state);
        var game = Assert.Single(state.Games);
        var configuration = Assert.Single(state.CycleConfigurations);
        var authoritativeGameCreatedAt = TestState.Now.AddDays(-40);
        var authoritativeFirstStartedAt = TestState.Now.AddDays(-20);
        var authoritativeEnrolledAt = TestState.Now.AddDays(-10);
        const string authoritativeGameName = "The Long Campaign";
        const string authoritativePolicyKey = "campaign-policy-v3";
        const int authoritativePolicyVersion = 3;
        var authoritativePolicyHash = new string('a', 64);
        game.Name = authoritativeGameName;
        game.GamePolicyKey = authoritativePolicyKey;
        game.GamePolicyVersion = authoritativePolicyVersion;
        game.GamePolicyContentHash = authoritativePolicyHash;
        game.PolicyProvenanceStatus = ProvenanceStatus.Verified;
        game.CreatedAt = authoritativeGameCreatedAt;
        game.FirstStartedAt = authoritativeFirstStartedAt;
        var authoritativeConfigurationId = Guid.NewGuid();
        configuration.CycleConfigurationId = authoritativeConfigurationId;
        cycle.CycleConfigurationId = authoritativeConfigurationId;
        foreach (var enrolment in state.GameEnrolments)
        {
            enrolment.GameEnrolmentId = Guid.NewGuid();
            enrolment.EnrolledAt = authoritativeEnrolledAt;
        }
        configuration.ScheduledStartAt = null;
        configuration.ScheduledEndAt = null;
        configuration.TickLengthMinutes = null;

        CycleEndService.CompleteCycle(state, cycle.CycleId, TestState.Now.AddHours(4));

        Assert.Equal(authoritativeGameCreatedAt, game.CreatedAt);
        Assert.Equal(authoritativeFirstStartedAt, game.FirstStartedAt);
        Assert.Equal(authoritativeGameName, game.Name);
        Assert.Equal(authoritativePolicyKey, game.GamePolicyKey);
        Assert.Equal(authoritativePolicyVersion, game.GamePolicyVersion);
        Assert.Equal(authoritativePolicyHash, game.GamePolicyContentHash);
        Assert.Equal(ProvenanceStatus.Verified, game.PolicyProvenanceStatus);
        Assert.All(state.GameEnrolments, enrolment => Assert.Equal(authoritativeEnrolledAt, enrolment.EnrolledAt));
        Assert.Null(configuration.ScheduledStartAt);
        Assert.Null(configuration.ScheduledEndAt);
        Assert.Null(configuration.TickLengthMinutes);
        AssertStatusChangedEvent(
            Assert.Single(state.GameLifecycleEvents, item => item.Type == GameLifecycleEventType.StatusChanged),
            cycle.CycleId,
            TestState.Now.AddHours(4),
            GameLifecycleStatus.Active,
            GameLifecycleStatus.Completed);
    }

    [Fact]
    public void CompleteCycleRejectsAlreadyCompletedCycle()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        cycle.Status = CycleStatus.Completed;

        var ex = Assert.Throws<InvalidOperationException>(() => CycleEndService.CompleteCycle(state, cycle.CycleId, TestState.Now));

        Assert.Contains("Only active cycles", ex.Message, StringComparison.Ordinal);
        Assert.Empty(state.CycleRankings);
    }

    [Fact]
    public void CompleteCycleIncreasesHistoricalSignificanceForBattleSystems()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 20);
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        var system = state.Systems.Single();
        var firstEmpire = state.Empires.Single(empire => empire.EmpireName == "First");
        var secondEmpire = state.Empires.Single(empire => empire.EmpireName == "Second");
        var smallerBattle = CreateBattle(cycle.CycleId, system.SystemId, firstEmpire.EmpireId, secondEmpire.EmpireId, attackerLosses: 3, defenderLosses: 4);
        var largerBattle = CreateBattle(cycle.CycleId, system.SystemId, firstEmpire.EmpireId, secondEmpire.EmpireId, attackerLosses: 12, defenderLosses: 8);
        state.BattleRecords.Add(smallerBattle);
        state.BattleRecords.Add(largerBattle);

        CycleEndService.CompleteCycle(state, cycle.CycleId, TestState.Now);

        Assert.Equal(3, system.HistoricalSignificance);
        var signal = Assert.Single(state.SystemHistoricalSignals);
        Assert.Equal(system.SystemId, signal.SystemId);
        Assert.Equal(SystemHistoricalSignalType.BattleActivity, signal.SignalType);
        Assert.Equal(largerBattle.BattleId, signal.SourceBattleId);
        Assert.Equal(2, signal.BattleCount);
        Assert.Equal(27, signal.TotalLosses);
        Assert.Equal(20, signal.LargestBattleLosses);
        Assert.True(signal.HostedCycleLargestBattle);
        Assert.Equal(3, signal.HistoricalSignificanceIncrease);
        Assert.Equal(3, signal.HistoricalSignificanceAfter);
        Assert.Equal(TestState.Now, signal.CreatedAt);
        Assert.Contains("Contest recorded 2 battles", signal.Summary, StringComparison.Ordinal);
        Assert.Contains("totalLosses", signal.FactJson, StringComparison.Ordinal);
        var completionEvent = Assert.Single(state.Events, item => item.EventType == EventType.CycleCompleted);
        Assert.Contains("historicalSignals", completionEvent.FactJson, StringComparison.Ordinal);
    }

    [Fact]
    public void CompleteCyclePreservesTopTenPercentOfBattlesAsMajorEvents()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 20);
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        var system = state.Systems.Single();
        var firstEmpire = state.Empires.Single(empire => empire.EmpireName == "First");
        var secondEmpire = state.Empires.Single(empire => empire.EmpireName == "Second");
        var battles = Enumerable.Range(1, 11)
            .Select(losses => CreateBattle(
                cycle.CycleId,
                system.SystemId,
                firstEmpire.EmpireId,
                secondEmpire.EmpireId,
                attackerLosses: losses,
                defenderLosses: 0,
                tickNumber: losses))
            .ToArray();
        state.BattleRecords.AddRange(battles);

        CycleEndService.CompleteCycle(state, cycle.CycleId, TestState.Now);

        var majorEvents = state.CycleMajorEvents.OrderBy(item => item.SelectionRank).ToArray();
        Assert.Equal(2, majorEvents.Length);
        Assert.Equal(battles[10].BattleId, majorEvents[0].SourceBattleId);
        Assert.Equal(11, majorEvents[0].TotalLosses);
        Assert.Equal(CycleMajorEventType.Battle, majorEvents[0].EventType);
        Assert.Equal(TestState.Now, majorEvents[0].CreatedAt);
        Assert.Contains("Battle at Contest", majorEvents[0].Summary, StringComparison.Ordinal);
        Assert.Contains("totalLosses", majorEvents[0].FactJson, StringComparison.Ordinal);
        Assert.Equal(battles[9].BattleId, majorEvents[1].SourceBattleId);
        Assert.Equal(10, majorEvents[1].TotalLosses);

        var completionEvent = Assert.Single(state.Events, item => item.EventType == EventType.CycleCompleted);
        Assert.Contains("majorEvents", completionEvent.FactJson, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateNextCyclePreservesHistoricalSystemsAndPlayerContinuity()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 20);
        var sourceCycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        var sourceSystem = state.Systems.Single();
        var firstEmpire = state.Empires.Single(empire => empire.EmpireName == "First");
        var secondEmpire = state.Empires.Single(empire => empire.EmpireName == "Second");
        state.BattleRecords.Add(CreateBattle(
            sourceCycle.CycleId,
            sourceSystem.SystemId,
            firstEmpire.EmpireId,
            secondEmpire.EmpireId,
            attackerLosses: 12,
            defenderLosses: 8));
        CycleEndService.CompleteCycle(state, sourceCycle.CycleId, TestState.Now);

        var result = CycleContinuityService.GenerateNextCycle(
            state,
            sourceCycle.CycleId,
            TestState.Now.AddDays(1),
            seed: 9876);

        var nextCycle = state.Cycles.Single(cycle => cycle.CycleId == result.CycleId);
        var nextSectors = state.Sectors.Where(sector => sector.CycleId == nextCycle.CycleId).ToArray();
        var nextSystems = state.Systems.Where(system => system.CycleId == nextCycle.CycleId).ToArray();
        var nextEmpires = state.Empires.Where(empire => empire.CycleId == nextCycle.CycleId).ToArray();
        var preservedSystem = Assert.Single(result.PreservedSystems);
        var seedEvent = Assert.Single(state.Events, item => item.CycleId == nextCycle.CycleId && item.EventType == EventType.CycleSeeded);

        Assert.Equal(CycleStatus.Active, nextCycle.Status);
        Assert.Equal(TestState.Now.AddDays(1), nextCycle.StartAt);
        Assert.Equal(CycleSchedulingMode.Scheduled, nextCycle.SchedulingMode);
        Assert.Equal(nextCycle.StartAt, nextCycle.NextTickAt);
        Assert.Equal(sourceCycle.CycleId, nextCycle.PreviousCycleId);
        Assert.Equal(GameFoundationConstants.LegacyGameId, nextCycle.GameId);
        Assert.Contains(state.CycleConfigurations, configuration =>
            configuration.CycleConfigurationId == nextCycle.CycleConfigurationId
            && configuration.GameId == nextCycle.GameId
            && configuration.SequenceNumber == 2
            && configuration.SchedulingMode == nextCycle.SchedulingMode);
        Assert.Equal(GameLifecycleStatus.Active, Assert.Single(state.Games).Status);
        Assert.Equal(9876, result.Seed);
        Assert.Equal(2, nextEmpires.Length);
        Assert.Equal(2, result.SuccessorEmpires.Count);
        Assert.NotEmpty(nextSectors);
        Assert.All(nextSystems, system => Assert.Contains(nextSectors, sector => sector.SectorId == system.SectorId));
        Assert.Equal(
            ["Aurelian Compact", "Khepri Mandate"],
            nextEmpires.Select(empire => empire.EmpireName).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(
            new[] { firstEmpire.PlayerId, secondEmpire.PlayerId }.Order().ToArray(),
            nextEmpires.Select(empire => empire.PlayerId).Order().ToArray());
        Assert.Equal(sourceSystem.SystemId, preservedSystem.SourceSystemId);
        Assert.Equal("Contest", preservedSystem.SystemName);
        Assert.Contains(nextSystems, system => system.SystemId == preservedSystem.NewSystemId
                                               && system.SystemName == "Contest"
                                               && system.HistoricalSignificance >= 2);
        Assert.Contains("sourceCycleId", seedEvent.FactJson, StringComparison.Ordinal);
        Assert.Contains("preservedSystems", seedEvent.FactJson, StringComparison.Ordinal);
        Assert.Contains("successorEmpires", seedEvent.FactJson, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateNextCycleResetsParticipantPowerIndependentlyOfPriorRank()
    {
        var completedState = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 20);
        var sourceCycle = completedState.GetActiveCycle()
            ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        var sourceSystem = completedState.Systems.Single();
        var firstEmpire = completedState.Empires.Single(empire => empire.EmpireName == "First");
        var secondEmpire = completedState.Empires.Single(empire => empire.EmpireName == "Second");
        var firstResources = completedState.EmpireResources.Single(resource => resource.EmpireId == firstEmpire.EmpireId);
        firstResources.Industry = 900;
        firstResources.Research = 800;
        firstResources.Population = 700;
        firstResources.LastGeneratedIndustry = 60;
        firstResources.LastSpentPopulation = 50;
        var firstPriorities = completedState.EmpirePriorities.Single(priority => priority.EmpireId == firstEmpire.EmpireId);
        firstPriorities.MilitaryWeight = 90;
        firstPriorities.ExpansionWeight = 10;
        completedState.EmpireDoctrineUnlocks.Add(new EmpireDoctrineUnlock
        {
            CycleId = sourceCycle.CycleId,
            EmpireId = firstEmpire.EmpireId,
            DoctrineKey = "survey-projection",
            UnlockedTickNumber = 3,
            UnlockedAt = TestState.Now
        });
        completedState.ColonialOutposts.Add(new ColonialOutpost
        {
            CycleId = sourceCycle.CycleId,
            EmpireId = firstEmpire.EmpireId,
            SystemId = sourceSystem.SystemId,
            EstablishedTick = 4,
            CreatedAt = TestState.Now
        });
        var sourceAdmiral = new Admiral
        {
            CycleId = sourceCycle.CycleId,
            EmpireId = firstEmpire.EmpireId,
            AdmiralName = "Prior Victor",
            ReputationScore = 75,
            CreatedAt = TestState.Now,
            UpdatedAt = TestState.Now
        };
        completedState.Admirals.Add(sourceAdmiral);
        completedState.Fleets.Single(fleet => fleet.EmpireId == firstEmpire.EmpireId).AdmiralId = sourceAdmiral.AdmiralId;
        completedState.BattleRecords.Add(CreateBattle(
            sourceCycle.CycleId,
            sourceSystem.SystemId,
            firstEmpire.EmpireId,
            secondEmpire.EmpireId,
            attackerLosses: 12,
            defenderLosses: 8));
        CycleEndService.CompleteCycle(completedState, sourceCycle.CycleId, TestState.Now);

        var firstWinnerState = completedState.DeepClone();
        var secondWinnerState = completedState.DeepClone();
        foreach (var ranking in secondWinnerState.CycleRankings.Where(item => item.CycleId == sourceCycle.CycleId))
        {
            ranking.IsWinner = ranking.EmpireId == secondEmpire.EmpireId;
            ranking.Rank = ranking.IsWinner ? 1 : 2;
        }

        var firstWinnerResult = CycleContinuityService.GenerateNextCycle(
            firstWinnerState,
            sourceCycle.CycleId,
            TestState.Now.AddDays(1),
            seed: 2468);
        var secondWinnerResult = CycleContinuityService.GenerateNextCycle(
            secondWinnerState,
            sourceCycle.CycleId,
            TestState.Now.AddDays(1),
            seed: 2468);

        Assert.Equal(
            firstWinnerResult.PreservedSystems.Select(SystemEcho).ToArray(),
            secondWinnerResult.PreservedSystems.Select(SystemEcho).ToArray());

        foreach (var playerId in new[] { firstEmpire.PlayerId, secondEmpire.PlayerId })
        {
            var firstSuccessor = AssertSuccessorReset(firstWinnerState, firstWinnerResult.CycleId, playerId);
            var secondSuccessor = AssertSuccessorReset(secondWinnerState, secondWinnerResult.CycleId, playerId);
            Assert.Equal(firstSuccessor.EmpireName, secondSuccessor.EmpireName);
            Assert.Equal(
                firstWinnerState.Systems.Single(system => system.SystemId == firstSuccessor.HomeSystemId).SystemName,
                secondWinnerState.Systems.Single(system => system.SystemId == secondSuccessor.HomeSystemId).SystemName);
        }
    }

    [Fact]
    public void GenerateNextCyclePreservesAuthoritativeFoundationMetadataAndPriorConfigurationSnapshot()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 20);
        var sourceCycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        LegacyGameFoundation.Apply(state);
        var importedConfiguration = Assert.Single(state.CycleConfigurations);
        var authoritativeConfigurationId = Guid.NewGuid();
        importedConfiguration.CycleConfigurationId = authoritativeConfigurationId;
        sourceCycle.CycleConfigurationId = authoritativeConfigurationId;
        foreach (var enrolment in state.GameEnrolments)
        {
            enrolment.GameEnrolmentId = Guid.NewGuid();
        }
        CycleEndService.CompleteCycle(state, sourceCycle.CycleId, TestState.Now.AddHours(4));
        var sourceConfigurationId = sourceCycle.CycleConfigurationId
            ?? throw new InvalidOperationException("Source Cycle must have a configuration.");
        var sourceConfiguration = state.CycleConfigurations.Single(item =>
            item.CycleConfigurationId == sourceConfigurationId);
        var authoritativeGameCreatedAt = TestState.Now.AddDays(-40);
        var authoritativeFirstStartedAt = TestState.Now.AddDays(-20);
        var authoritativeEnrolledAt = TestState.Now.AddDays(-10);
        const string authoritativeGameName = "The Long Campaign";
        const string authoritativePolicyKey = "campaign-policy-v3";
        const int authoritativePolicyVersion = 3;
        var authoritativePolicyHash = new string('a', 64);
        var authoritativeGame = Assert.Single(state.Games);
        authoritativeGame.Name = authoritativeGameName;
        authoritativeGame.GamePolicyKey = authoritativePolicyKey;
        authoritativeGame.GamePolicyVersion = authoritativePolicyVersion;
        authoritativeGame.GamePolicyContentHash = authoritativePolicyHash;
        authoritativeGame.PolicyProvenanceStatus = ProvenanceStatus.Verified;
        authoritativeGame.CreatedAt = authoritativeGameCreatedAt;
        authoritativeGame.FirstStartedAt = authoritativeFirstStartedAt;
        foreach (var enrolment in state.GameEnrolments)
        {
            enrolment.EnrolledAt = authoritativeEnrolledAt;
        }
        sourceConfiguration.ScheduledStartAt = null;
        sourceConfiguration.ScheduledEndAt = null;
        sourceConfiguration.TickLengthMinutes = null;

        var result = CycleContinuityService.GenerateNextCycle(
            state,
            sourceCycle.CycleId,
            TestState.Now.AddDays(1),
            seed: 9876);

        var game = Assert.Single(state.Games);
        sourceConfiguration = state.CycleConfigurations.Single(item =>
            item.CycleConfigurationId == sourceConfigurationId);
        Assert.Equal(authoritativeGameCreatedAt, game.CreatedAt);
        Assert.Equal(authoritativeFirstStartedAt, game.FirstStartedAt);
        Assert.Equal(authoritativeGameName, game.Name);
        Assert.Equal(authoritativePolicyKey, game.GamePolicyKey);
        Assert.Equal(authoritativePolicyVersion, game.GamePolicyVersion);
        Assert.Equal(authoritativePolicyHash, game.GamePolicyContentHash);
        Assert.Equal(ProvenanceStatus.Verified, game.PolicyProvenanceStatus);
        Assert.Null(game.CompletedAt);
        Assert.All(state.GameEnrolments, enrolment =>
        {
            Assert.Equal(authoritativeEnrolledAt, enrolment.EnrolledAt);
            Assert.Equal(GameEnrolmentStatus.Enrolled, enrolment.Status);
            Assert.Null(enrolment.EndedAt);
        });
        Assert.Null(sourceConfiguration.ScheduledStartAt);
        Assert.Null(sourceConfiguration.ScheduledEndAt);
        Assert.Null(sourceConfiguration.TickLengthMinutes);
        Assert.Contains(state.CycleConfigurations, configuration =>
            configuration.CycleConfigurationId == result.CycleId
            && configuration.SequenceNumber == 2);
        Assert.Collection(
            state.GameLifecycleEvents
                .Where(item => item.Type == GameLifecycleEventType.StatusChanged)
                .OrderBy(item => item.CreatedAt),
            gameEvent => AssertStatusChangedEvent(
                gameEvent,
                sourceCycle.CycleId,
                TestState.Now.AddHours(4),
                GameLifecycleStatus.Active,
                GameLifecycleStatus.Completed),
            gameEvent => AssertStatusChangedEvent(
                gameEvent,
                result.CycleId,
                TestState.Now.AddDays(1),
                GameLifecycleStatus.Completed,
                GameLifecycleStatus.Active));
        LegacyGameFoundation.ApplyLifecycleTransition(state);
        Assert.Equal(2, state.GameLifecycleEvents.Count(item =>
            item.Type == GameLifecycleEventType.StatusChanged));
    }

    [Fact]
    public void GenerateNextCycleRetainsCanonicalSectorScale()
    {
        var state = GameSeeder.CreateCuratedColdStart(TestState.Now);
        var sourceCycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        CycleEndService.CompleteCycle(state, sourceCycle.CycleId, TestState.Now);

        var result = CycleContinuityService.GenerateNextCycle(
            state,
            sourceCycle.CycleId,
            TestState.Now.AddDays(1),
            seed: 9876);

        var sectors = state.Sectors.Where(sector => sector.CycleId == result.CycleId).ToArray();
        var systems = state.Systems.Where(system => system.CycleId == result.CycleId).ToArray();
        Assert.Equal(GameSeeder.CanonicalGalaxySectorCount, sectors.Length);
        Assert.Equal(GameSeeder.CanonicalGalaxySystemCount, systems.Length);
        Assert.All(sectors, sector => Assert.Equal(
            GameSeeder.CanonicalGalaxySystemCount / GameSeeder.CanonicalGalaxySectorCount,
            systems.Count(system => system.SectorId == sector.SectorId)));
    }

    [Fact]
    public void GenerateNextCycleRejectsCycleThatIsNotCompleted()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");

        var ex = Assert.Throws<InvalidOperationException>(() => CycleContinuityService.GenerateNextCycle(
            state,
            cycle.CycleId,
            TestState.Now.AddDays(1)));

        Assert.Contains("Only completed Cycles", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateNextCycleRejectsWhenAnotherCycleIsActive()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 20);
        var sourceCycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        CycleEndService.CompleteCycle(state, sourceCycle.CycleId, TestState.Now);
        state.Cycles.Add(new Cycle
        {
            Name = "Already active",
            StartAt = TestState.Now,
            EndAt = TestState.Now.AddDays(90),
            Status = CycleStatus.Active,
            CreatedAt = TestState.Now
        });

        var ex = Assert.Throws<InvalidOperationException>(() => CycleContinuityService.GenerateNextCycle(
            state,
            sourceCycle.CycleId,
            TestState.Now.AddDays(1)));

        Assert.Contains("another Cycle is active", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateNextCycleRejectsWhenAnotherCycleRequiresRecoveryWithoutChangingState()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 20);
        var sourceCycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        CycleEndService.CompleteCycle(state, sourceCycle.CycleId, TestState.Now);
        state.Cycles.Add(new Cycle
        {
            Name = "Recovery required",
            StartAt = TestState.Now,
            EndAt = TestState.Now.AddDays(90),
            Status = CycleStatus.RecoveryRequired,
            CreatedAt = TestState.Now
        });
        var cycles = state.Cycles;
        var before = Snapshot(state);

        var ex = Assert.Throws<InvalidOperationException>(() => CycleContinuityService.GenerateNextCycle(
            state,
            sourceCycle.CycleId,
            TestState.Now.AddDays(1)));

        Assert.Contains("requires recovery", ex.Message, StringComparison.Ordinal);
        Assert.Same(cycles, state.Cycles);
        Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void GenerateNextCycleRejectsSecondSuccessorWithoutChangingState()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 20);
        var sourceCycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        CycleEndService.CompleteCycle(state, sourceCycle.CycleId, TestState.Now);
        var firstSuccessor = CycleContinuityService.GenerateNextCycle(
            state,
            sourceCycle.CycleId,
            TestState.Now.AddDays(1),
            seed: 1234);
        CycleEndService.CompleteCycle(state, firstSuccessor.CycleId, TestState.Now.AddDays(2));
        var cycles = state.Cycles;
        var before = Snapshot(state);

        var ex = Assert.Throws<InvalidOperationException>(() => CycleContinuityService.GenerateNextCycle(
            state,
            sourceCycle.CycleId,
            TestState.Now.AddDays(3),
            seed: 5678));

        Assert.Contains("already has a successor", ex.Message, StringComparison.Ordinal);
        Assert.Same(cycles, state.Cycles);
        Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void GenerateNextCycleRollsBackWhenLifecycleTransitionIsTerminal()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 20);
        var sourceCycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        CycleEndService.CompleteCycle(state, sourceCycle.CycleId, TestState.Now);
        var game = Assert.Single(state.Games);
        game.Status = GameLifecycleStatus.Cancelled;
        game.CompletedAt = null;
        game.CancelledAt = TestState.Now;
        var cycles = state.Cycles;
        var before = Snapshot(state);

        var ex = Assert.Throws<InvalidOperationException>(() => CycleContinuityService.GenerateNextCycle(
            state,
            sourceCycle.CycleId,
            TestState.Now.AddDays(1),
            seed: 9876));

        Assert.Contains("cannot transition after cancellation", ex.Message, StringComparison.Ordinal);
        Assert.Same(cycles, state.Cycles);
        Assert.Equal(before, Snapshot(state));
    }

    private static string Snapshot(GameState state) =>
        JsonSerializer.Serialize(state, GameStateJson.Options);

    private static Empire AssertSuccessorReset(GameState state, Guid cycleId, Guid playerId)
    {
        var empire = state.Empires.Single(item => item.CycleId == cycleId && item.PlayerId == playerId);
        Assert.DoesNotContain("Legacy", empire.EmpireName, StringComparison.Ordinal);
        Assert.DoesNotContain("Remnant", empire.EmpireName, StringComparison.Ordinal);

        var resources = state.EmpireResources.Single(item => item.EmpireId == empire.EmpireId);
        Assert.Equal(100, resources.Industry);
        Assert.Equal(100, resources.Research);
        Assert.Equal(100, resources.Population);
        Assert.Equal(0, resources.LastGeneratedIndustry);
        Assert.Equal(0, resources.LastGeneratedResearch);
        Assert.Equal(0, resources.LastGeneratedPopulation);
        Assert.Equal(0, resources.LastSpentIndustry);
        Assert.Equal(0, resources.LastSpentResearch);
        Assert.Equal(0, resources.LastSpentPopulation);

        var priorities = state.EmpirePriorities.Single(item => item.EmpireId == empire.EmpireId);
        Assert.Equal(0, priorities.IndustryWeight);
        Assert.Equal(0, priorities.ResearchWeight);
        Assert.Equal(StrategicPriorityPolicy.DefaultMilitaryWeight, priorities.MilitaryWeight);
        Assert.Equal(StrategicPriorityPolicy.DefaultExpansionWeight, priorities.ExpansionWeight);
        Assert.DoesNotContain(state.EmpireDoctrineUnlocks, item => item.CycleId == cycleId && item.EmpireId == empire.EmpireId);
        Assert.DoesNotContain(state.ColonialOutposts, item => item.CycleId == cycleId && item.EmpireId == empire.EmpireId);

        var fleet = Assert.Single(state.Fleets, item => item.CycleId == cycleId && item.EmpireId == empire.EmpireId);
        Assert.Equal(60, fleet.ShipCount);
        Assert.Null(fleet.DestinationSystemId);
        Assert.Null(fleet.DepartureTickNumber);
        Assert.Null(fleet.ArrivalTickNumber);
        var admiral = Assert.Single(state.Admirals, item => item.CycleId == cycleId && item.EmpireId == empire.EmpireId);
        Assert.Equal(0, admiral.ReputationScore);
        Assert.Equal(admiral.AdmiralId, fleet.AdmiralId);
        Assert.Equal(MatchParticipantStatus.Active, state.MatchParticipants.Single(item =>
            item.CycleId == cycleId && item.PlayerId == playerId && item.EmpireId == empire.EmpireId).Status);
        return empire;
    }

    private static (string Name, int HistoricalSignificance, int StrategicValue, Guid SourceSystemId, Guid? SourceSignalId, int? SourceMajorEventRank) SystemEcho(
        PreservedSystemContinuity system) =>
        (
            system.SystemName,
            system.HistoricalSignificance,
            system.StrategicValue,
            system.SourceSystemId,
            system.SourceSignalId,
            system.SourceMajorEventRank);

    private static void AssertStatusChangedEvent(
        GameLifecycleEvent gameEvent,
        Guid cycleId,
        DateTimeOffset transitionAt,
        GameLifecycleStatus fromStatus,
        GameLifecycleStatus toStatus)
    {
        Assert.Equal(GameLifecycleEventType.StatusChanged, gameEvent.Type);
        Assert.Equal(fromStatus.ToString(), gameEvent.FromStatus);
        Assert.Equal(toStatus.ToString(), gameEvent.ToStatus);
        Assert.Equal(transitionAt, gameEvent.CreatedAt);
        using var fact = JsonDocument.Parse(gameEvent.FactJson);
        Assert.Equal(cycleId, fact.RootElement.GetProperty("cycleId").GetGuid());
        Assert.Equal(transitionAt, fact.RootElement.GetProperty("transitionAt").GetDateTimeOffset());
    }

    private static BattleRecord CreateBattle(
        Guid cycleId,
        Guid systemId,
        Guid attackerEmpireId,
        Guid defenderEmpireId,
        int attackerLosses,
        int defenderLosses,
        int tickNumber = 1) =>
        new()
        {
            CycleId = cycleId,
            TickNumber = tickNumber,
            SystemId = systemId,
            AttackerEmpireId = attackerEmpireId,
            DefenderEmpireId = defenderEmpireId,
            AttackerFleetIds = Guid.NewGuid().ToString(),
            DefenderFleetIds = Guid.NewGuid().ToString(),
            AttackerShipsBefore = 80,
            DefenderShipsBefore = 20,
            AttackerLosses = attackerLosses,
            DefenderLosses = defenderLosses,
            Outcome = BattleOutcome.AttackerVictory,
            FactJson = "{}",
            CreatedAt = TestState.Now
        };
}
