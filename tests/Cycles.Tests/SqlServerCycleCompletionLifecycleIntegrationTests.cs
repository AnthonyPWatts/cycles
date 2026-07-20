using Cycles.Core;
using Cycles.Infrastructure.SqlServer;

namespace Cycles.Tests;

[Collection(SqlServerIntegrationCollection.CollectionName)]
[Trait(SqlIntegrationGuard.CategoryName, SqlIntegrationGuard.CategoryValue)]
public sealed class SqlServerCycleCompletionLifecycleIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Focused_completion_persists_non_legacy_game_lifecycle_and_prior_only_enrolment()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var state = CreateNonLegacySuccessorState(out var gameId, out var cycleId, out var priorOnlyPlayerId);
        var store = new SqlServerGameStateStore(connectionString, () => new GameState());
        store.Replace(state);
        var cutoffAt = Now.AddDays(2);

        store.CompleteCycle(cycleId, cutoffAt);

        var persisted = store.LoadOrCreate();
        var game = persisted.Games.Single(item => item.GameId == gameId);
        Assert.Equal(GameLifecycleStatus.Completed, game.Status);
        Assert.Equal(cutoffAt, game.CompletedAt);

        var gameEvent = Assert.Single(persisted.GameLifecycleEvents);
        Assert.Equal(gameId, gameEvent.GameId);
        Assert.Equal(GameLifecycleEventType.StatusChanged, gameEvent.Type);
        Assert.Equal(GameLifecycleStatus.Active.ToString(), gameEvent.FromStatus);
        Assert.Equal(GameLifecycleStatus.Completed.ToString(), gameEvent.ToStatus);
        Assert.Equal(cutoffAt, gameEvent.CreatedAt);

        Assert.Single(persisted.MatchParticipants, item => item.PlayerId == priorOnlyPlayerId);
        var enrolment = persisted.GameEnrolments.Single(item =>
            item.GameId == gameId
            && item.PlayerId == priorOnlyPlayerId);
        Assert.Equal(GameEnrolmentStatus.Completed, enrolment.Status);
        Assert.Equal(cutoffAt, enrolment.StatusChangedAt);
        Assert.Equal(cutoffAt, enrolment.EndedAt);
    }

    private static GameState CreateNonLegacySuccessorState(
        out Guid gameId,
        out Guid cycleId,
        out Guid priorOnlyPlayerId)
    {
        var state = GameSeeder.CreateDefault(
            systemCount: 8,
            empireCount: 3,
            seed: 7821,
            createdAt: Now);
        var sourceCycle = state.GetActiveCycle()
            ?? throw new InvalidOperationException("Seeded state must contain an active Cycle.");
        var sourceParticipants = state.MatchParticipants
            .Where(item => item.CycleId == sourceCycle.CycleId)
            .OrderBy(item => item.PlayerId)
            .ToArray();
        var selectedPriorOnlyPlayerId = sourceParticipants[^1].PlayerId;
        CycleEndService.CompleteCycle(state, sourceCycle.CycleId, Now.AddHours(1));
        var successor = CycleContinuityService.GenerateNextCycle(
            state,
            sourceCycle.CycleId,
            Now.AddDays(1),
            seed: 9917);
        var selectedCycleId = successor.CycleId;
        state.MatchParticipants.RemoveAll(item =>
            item.CycleId == selectedCycleId
            && item.PlayerId == selectedPriorOnlyPlayerId);
        var priorOnlyEnrolment = state.GameEnrolments.Single(item =>
            item.PlayerId == selectedPriorOnlyPlayerId);
        priorOnlyEnrolment.Status = GameEnrolmentStatus.Historical;
        priorOnlyEnrolment.StatusChangedAt = Now.AddHours(1);
        priorOnlyEnrolment.EndedAt = null;

        gameId = Guid.NewGuid();
        var game = Assert.Single(state.Games);
        game.GameId = gameId;
        game.Name = "Non-legacy lifecycle integration Game";
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
        cycleId = selectedCycleId;
        priorOnlyPlayerId = selectedPriorOnlyPlayerId;
        return state;
    }
}
