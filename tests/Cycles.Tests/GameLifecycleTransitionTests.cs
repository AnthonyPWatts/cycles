using System.Text.Json;
using Cycles.Core;

namespace Cycles.Tests;

public sealed class GameLifecycleTransitionTests
{
    [Fact]
    public void Complete_cycle_transitions_only_its_non_legacy_game()
    {
        var fixture = CreateNonLegacyGameWithPriorParticipant();
        var unrelatedGame = new Game
        {
            GameId = Guid.NewGuid(),
            Name = "Unrelated Game",
            Status = GameLifecycleStatus.Active,
            CreatedAt = TestState.Now
        };
        fixture.State.Games.Add(unrelatedGame);
        fixture.State.Cycles.Add(new Cycle
        {
            GameId = unrelatedGame.GameId,
            Name = "Unrelated Cycle",
            StartAt = TestState.Now,
            EndAt = TestState.Now.AddDays(30),
            Status = CycleStatus.Active,
            CreatedAt = TestState.Now
        });
        var cutoffAt = TestState.Now.AddHours(4);

        CycleEndService.CompleteCycle(fixture.State, fixture.CycleId, cutoffAt);

        var game = fixture.State.Games.Single(item => item.GameId == fixture.GameId);
        Assert.NotEqual(GameFoundationConstants.LegacyGameId, game.GameId);
        Assert.Equal(GameLifecycleStatus.Completed, game.Status);
        Assert.Equal(cutoffAt, game.CompletedAt);
        Assert.Equal(GameLifecycleStatus.Active, unrelatedGame.Status);
        Assert.Null(unrelatedGame.CompletedAt);

        var gameEvent = Assert.Single(fixture.State.GameLifecycleEvents);
        Assert.Equal(fixture.GameId, gameEvent.GameId);
        Assert.Equal(GameLifecycleEventType.StatusChanged, gameEvent.Type);
        Assert.Equal(GameLifecycleStatus.Active.ToString(), gameEvent.FromStatus);
        Assert.Equal(GameLifecycleStatus.Completed.ToString(), gameEvent.ToStatus);
        Assert.Equal(cutoffAt, gameEvent.CreatedAt);
        using var fact = JsonDocument.Parse(gameEvent.FactJson);
        Assert.Equal(fixture.CycleId, fact.RootElement.GetProperty("cycleId").GetGuid());
    }

    [Fact]
    public void Complete_cycle_completes_prior_cycle_only_enrolment()
    {
        var fixture = CreateNonLegacyGameWithPriorParticipant();
        var cutoffAt = TestState.Now.AddHours(4);

        CycleEndService.CompleteCycle(fixture.State, fixture.CycleId, cutoffAt);

        var enrolment = fixture.State.GameEnrolments.Single(item =>
            item.GameId == fixture.GameId
            && item.PlayerId == fixture.PriorOnlyPlayerId);
        Assert.Equal(GameEnrolmentStatus.Completed, enrolment.Status);
        Assert.Equal(cutoffAt, enrolment.StatusChangedAt);
        Assert.Equal(cutoffAt, enrolment.EndedAt);
    }

    [Fact]
    public void Complete_cycle_completes_an_enrolment_that_never_had_a_participant()
    {
        var fixture = CreateNonLegacyGameWithPriorParticipant();
        var lobbyPlayer = AddEnrolmentWithoutParticipant(fixture);
        var cutoffAt = TestState.Now.AddHours(4);

        CycleEndService.CompleteCycle(fixture.State, fixture.CycleId, cutoffAt);

        var enrolment = fixture.State.GameEnrolments.Single(item =>
            item.GameId == fixture.GameId
            && item.PlayerId == lobbyPlayer.PlayerId);
        Assert.Equal(GameEnrolmentStatus.Completed, enrolment.Status);
        Assert.Equal(cutoffAt, enrolment.StatusChangedAt);
        Assert.Equal(cutoffAt, enrolment.EndedAt);
    }

    [Fact]
    public void Successor_activation_makes_a_zero_participant_enrolment_historical()
    {
        var fixture = CreateNonLegacyGameWithPriorParticipant();
        var lobbyPlayer = AddEnrolmentWithoutParticipant(fixture);
        var cutoffAt = TestState.Now.AddHours(4);
        CycleEndService.CompleteCycle(fixture.State, fixture.CycleId, cutoffAt);
        fixture.State.Cycles.Add(new Cycle
        {
            GameId = fixture.GameId,
            Name = "Successor Cycle",
            StartAt = cutoffAt.AddDays(1),
            EndAt = cutoffAt.AddDays(31),
            Status = CycleStatus.Active,
            CreatedAt = cutoffAt.AddDays(1)
        });

        GameLifecycleTransitions.ApplyCycleState(fixture.State, fixture.GameId);

        var enrolment = fixture.State.GameEnrolments.Single(item =>
            item.GameId == fixture.GameId
            && item.PlayerId == lobbyPlayer.PlayerId);
        Assert.Equal(GameEnrolmentStatus.Historical, enrolment.Status);
        Assert.Equal(cutoffAt, enrolment.StatusChangedAt);
        Assert.Null(enrolment.EndedAt);
    }

    private static Player AddEnrolmentWithoutParticipant(LifecycleFixture fixture)
    {
        var player = new Player
        {
            Username = $"lobby-{Guid.NewGuid():N}",
            Status = PlayerStatus.Active,
            CreatedAt = TestState.Now.AddDays(-2)
        };
        fixture.State.Players.Add(player);
        fixture.State.GameEnrolments.Add(new GameEnrolment
        {
            GameId = fixture.GameId,
            PlayerId = player.PlayerId,
            Status = GameEnrolmentStatus.Enrolled,
            Origin = GameEnrolmentOrigin.Direct,
            EnrolledAt = TestState.Now.AddDays(-1),
            StatusChangedAt = TestState.Now.AddDays(-1)
        });
        return player;
    }

    private static LifecycleFixture CreateNonLegacyGameWithPriorParticipant()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 20);
        LegacyGameFoundation.Apply(state);
        var gameId = Guid.NewGuid();
        var game = Assert.Single(state.Games);
        game.GameId = gameId;
        game.Name = "Non-legacy Game";
        foreach (var cycle in state.Cycles)
        {
            cycle.GameId = gameId;
        }
        foreach (var configuration in state.CycleConfigurations)
        {
            configuration.GameId = gameId;
        }
        foreach (var enrolment in state.GameEnrolments)
        {
            enrolment.GameId = gameId;
        }
        foreach (var participant in state.MatchParticipants)
        {
            participant.GameId = gameId;
        }
        state.GameLifecycleEvents.Clear();

        var activeCycle = state.GetActiveCycle()
            ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        var priorCycle = new Cycle
        {
            GameId = gameId,
            Name = "Prior Cycle",
            StartAt = TestState.Now.AddDays(-20),
            EndAt = TestState.Now.AddDays(-10),
            Status = CycleStatus.Completed,
            CreatedAt = TestState.Now.AddDays(-20)
        };
        state.Cycles.Add(priorCycle);
        var priorPlayer = new Player
        {
            Username = $"prior-{Guid.NewGuid():N}",
            Status = PlayerStatus.Active,
            CreatedAt = priorCycle.StartAt
        };
        state.Players.Add(priorPlayer);
        state.MatchParticipants.Add(new MatchParticipant
        {
            GameId = gameId,
            CycleId = priorCycle.CycleId,
            PlayerId = priorPlayer.PlayerId,
            EmpireId = Guid.NewGuid(),
            Status = MatchParticipantStatus.Completed,
            JoinedAt = priorCycle.StartAt,
            EndedAt = priorCycle.EndAt
        });
        state.GameEnrolments.Add(new GameEnrolment
        {
            GameId = gameId,
            PlayerId = priorPlayer.PlayerId,
            Status = GameEnrolmentStatus.Historical,
            Origin = GameEnrolmentOrigin.Direct,
            EnrolledAt = priorCycle.StartAt,
            StatusChangedAt = priorCycle.EndAt
        });

        return new LifecycleFixture(state, gameId, activeCycle.CycleId, priorPlayer.PlayerId);
    }

    private sealed record LifecycleFixture(
        GameState State,
        Guid GameId,
        Guid CycleId,
        Guid PriorOnlyPlayerId);
}
