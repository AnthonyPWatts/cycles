using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cycles.Core;

public static class GameLifecycleTransitions
{
    public static void ApplyCycleState(GameState state, Guid gameId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (gameId == Guid.Empty)
        {
            throw new ArgumentException("Game identifier cannot be empty.", nameof(gameId));
        }

        var game = state.Games.SingleOrDefault(item => item.GameId == gameId)
            ?? throw new InvalidOperationException($"Game {gameId} was not found.");
        if (game.Status is GameLifecycleStatus.Cancelled or GameLifecycleStatus.Terminated
            || game.CancelledAt.HasValue
            || game.TerminatedAt.HasValue)
        {
            throw new InvalidOperationException(
                "A Game cannot transition after cancellation or termination.");
        }

        var cycles = state.Cycles
            .Where(cycle => cycle.GameId == gameId)
            .ToArray();
        if (cycles.Length == 0)
        {
            throw new InvalidOperationException($"Game {gameId} has no Cycles.");
        }

        var operationalCycles = cycles
            .Where(cycle => cycle.Status is CycleStatus.Active or CycleStatus.RecoveryRequired)
            .ToArray();
        if (operationalCycles.Length > 1)
        {
            throw new InvalidOperationException(
                $"Game {gameId} has more than one operational Cycle.");
        }

        var previousStatus = game.Status;
        var nextStatus = operationalCycles.Length == 1
            ? GameLifecycleStatus.Active
            : GameLifecycleStatus.Completed;
        DateTimeOffset? completedAt = nextStatus == GameLifecycleStatus.Completed
            ? cycles.Max(cycle => cycle.EndAt)
            : null;
        var transitionCycle = nextStatus == GameLifecycleStatus.Active
            ? operationalCycles[0]
            : cycles
                .OrderByDescending(cycle => cycle.EndAt)
                .ThenBy(cycle => cycle.CycleId)
                .First();
        var transitionAt = nextStatus == GameLifecycleStatus.Active
            ? transitionCycle.StartAt
            : completedAt!.Value;

        game.Status = nextStatus;
        game.CompletedAt = completedAt;

        var participantsByPlayer = state.MatchParticipants
            .Where(participant => participant.GameId == gameId)
            .ToLookup(participant => participant.PlayerId);
        var enrolments = state.GameEnrolments
            .Where(enrolment => enrolment.GameId == gameId)
            .OrderBy(enrolment => enrolment.PlayerId)
            .ToArray();
        var enrolmentPlayerIds = enrolments
            .Select(enrolment => enrolment.PlayerId)
            .ToHashSet();
        var participantWithoutEnrolment = state.MatchParticipants.FirstOrDefault(participant =>
            participant.GameId == gameId
            && !enrolmentPlayerIds.Contains(participant.PlayerId));
        if (participantWithoutEnrolment is not null)
        {
            throw new InvalidOperationException(
                $"Game {gameId} has no enrolment for participant player {participantWithoutEnrolment.PlayerId}.");
        }

        foreach (var enrolment in enrolments)
        {
            var participants = participantsByPlayer[enrolment.PlayerId]
                .OrderByDescending(participant => participant.JoinedAt)
                .ThenBy(participant => participant.MatchParticipantId)
                .ToArray();
            var operationalParticipant = operationalCycles.Length == 0
                ? null
                : participants.SingleOrDefault(participant =>
                    participant.CycleId == operationalCycles[0].CycleId);
            var status = ResolveEnrolmentStatus(operationalParticipant, game.Status);
            var latestParticipationAt = participants.Length == 0
                ? (enrolment.StatusChangedAt > enrolment.EnrolledAt
                    ? enrolment.StatusChangedAt
                    : enrolment.EnrolledAt)
                : participants.Max(participant => participant.EndedAt ?? participant.JoinedAt);
            var statusChangedAt = ResolveStatusChangedAt(
                latestParticipationAt,
                operationalParticipant,
                game,
                status);

            enrolment.Status = status;
            enrolment.StatusChangedAt = statusChangedAt;
            enrolment.EndedAt = status is GameEnrolmentStatus.Completed or GameEnrolmentStatus.Withdrawn
                ? statusChangedAt
                : null;
        }

        if (previousStatus != nextStatus)
        {
            AppendStatusChangedEvent(
                state,
                gameId,
                transitionCycle.CycleId,
                transitionAt,
                previousStatus,
                nextStatus);
        }
    }

    private static GameEnrolmentStatus ResolveEnrolmentStatus(
        MatchParticipant? operationalParticipant,
        GameLifecycleStatus gameStatus)
    {
        if (gameStatus == GameLifecycleStatus.Completed)
        {
            return GameEnrolmentStatus.Completed;
        }

        if (operationalParticipant is null)
        {
            return GameEnrolmentStatus.Historical;
        }

        if (operationalParticipant.Status == MatchParticipantStatus.Withdrawn)
        {
            return GameEnrolmentStatus.Withdrawn;
        }

        return GameEnrolmentStatus.Enrolled;
    }

    private static DateTimeOffset ResolveStatusChangedAt(
        DateTimeOffset latestParticipationAt,
        MatchParticipant? operationalParticipant,
        Game game,
        GameEnrolmentStatus status) => status switch
        {
            GameEnrolmentStatus.Completed => game.CompletedAt
                ?? throw new InvalidOperationException("A completed Game must have a completion timestamp."),
            GameEnrolmentStatus.Enrolled or GameEnrolmentStatus.Withdrawn =>
                (operationalParticipant ?? throw new InvalidOperationException("Operational enrolment has no participant."))
                .EndedAt ?? operationalParticipant.JoinedAt,
            _ => latestParticipationAt
        };

    private static void AppendStatusChangedEvent(
        GameState state,
        Guid gameId,
        Guid cycleId,
        DateTimeOffset transitionAt,
        GameLifecycleStatus fromStatus,
        GameLifecycleStatus toStatus)
    {
        var factJson = CreateFactJson(
            "cycle-derived-game-lifecycle-transition",
            cycleId,
            transitionAt,
            fromStatus,
            toStatus);
        var legacyFactJson = CreateFactJson(
            "legacy-game-lifecycle-transition",
            cycleId,
            transitionAt,
            fromStatus,
            toStatus);
        var eventId = CreateStatusChangedEventId(
            gameId,
            cycleId,
            transitionAt,
            fromStatus,
            toStatus);
        var existing = state.GameLifecycleEvents.SingleOrDefault(item =>
            item.GameLifecycleEventId == eventId);
        if (existing is not null)
        {
            if (existing.GameId != gameId
                || existing.Type != GameLifecycleEventType.StatusChanged
                || existing.FromStatus != fromStatus.ToString()
                || existing.ToStatus != toStatus.ToString()
                || existing.FactJson != factJson && existing.FactJson != legacyFactJson
                || existing.CreatedAt != transitionAt)
            {
                throw new InvalidOperationException(
                    $"Game lifecycle event {eventId} conflicts with the deterministic status transition audit.");
            }
            return;
        }

        state.GameLifecycleEvents.Add(new GameLifecycleEvent
        {
            GameLifecycleEventId = eventId,
            GameId = gameId,
            Type = GameLifecycleEventType.StatusChanged,
            FromStatus = fromStatus.ToString(),
            ToStatus = toStatus.ToString(),
            FactJson = factJson,
            CreatedAt = transitionAt
        });
    }

    private static string CreateFactJson(
        string source,
        Guid cycleId,
        DateTimeOffset transitionAt,
        GameLifecycleStatus fromStatus,
        GameLifecycleStatus toStatus) =>
        JsonSerializer.Serialize(new
        {
            source,
            schemaVersion = 1,
            cycleId,
            transitionAt,
            fromStatus = fromStatus.ToString(),
            toStatus = toStatus.ToString()
        }, GameStateJson.Options);

    private static Guid CreateStatusChangedEventId(
        Guid gameId,
        Guid cycleId,
        DateTimeOffset transitionAt,
        GameLifecycleStatus fromStatus,
        GameLifecycleStatus toStatus)
    {
        var identity = string.Join(
            '|',
            "cycles-game-status-v1",
            gameId.ToString("D"),
            cycleId.ToString("D"),
            transitionAt.ToString("O", CultureInfo.InvariantCulture),
            fromStatus.ToString(),
            toStatus.ToString());
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return new Guid(hash.AsSpan(0, 16));
    }
}
