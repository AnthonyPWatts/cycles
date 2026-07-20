using Cycles.Core;

namespace Cycles.Application;

public enum TutorialRunStatus
{
    Active,
    Paused,
    Completed,
    Skipped,
    Superseded
}

public sealed record TutorialRunSnapshot(
    Guid TutorialRunId,
    Guid GameId,
    Guid CycleId,
    Guid PlayerId,
    string TutorialKey,
    int DefinitionVersion,
    TutorialRunStatus Status,
    DateTimeOffset StartedAt,
    Guid? SupersededByTutorialRunId,
    DateTimeOffset? EndedAt);

public sealed record TutorialEvidenceSnapshot(
    bool Satisfied,
    string Summary,
    IReadOnlyList<Guid> FactIds);

public sealed record TutorialAcknowledgementSnapshot(
    string? Key,
    bool Required,
    bool Satisfied);

public sealed record TutorialLessonSnapshot(
    string Key,
    string Title,
    string Objective,
    string Hint,
    string EntryState,
    TutorialEvidenceSnapshot MechanicalEvidence,
    TutorialAcknowledgementSnapshot PresentationAcknowledgement,
    string CompletionState,
    string? BlockedReason,
    IReadOnlyList<string> AllowedRecoveryActions);

public sealed record TutorialJourneySnapshot(
    TutorialRunSnapshot Run,
    string JourneyName,
    string JourneyStatus,
    int CurrentTickNumber,
    TutorialLessonSnapshot? CurrentLesson,
    IReadOnlyList<TutorialLessonSnapshot> Lessons,
    bool CoreCompleted,
    bool CanResolve,
    bool CanStartFresh);

public sealed record TutorialAcknowledgementCommand(
    Guid PlayerId,
    Guid GameId,
    string AcknowledgementKey,
    DateTimeOffset AcknowledgedAt);

public sealed record TutorialStatusCommand(
    Guid PlayerId,
    Guid GameId,
    TutorialRunStatus Status,
    DateTimeOffset ChangedAt);

public sealed record FreshTrainingAttemptCommand(
    Guid PlayerId,
    Guid GameId,
    Guid RequestId,
    DateTimeOffset RequestedAt);

public sealed record FreshTrainingAttemptSnapshot(
    Guid TutorialRunId,
    Guid GameId,
    Guid CycleId,
    bool Created);

public interface ITutorialAttemptStore
{
    TutorialAttemptResult<TutorialJourneySnapshot> GetJourney(Guid playerId, Guid gameId);

    TutorialAttemptResult<TutorialJourneySnapshot> Acknowledge(
        TutorialAcknowledgementCommand command);

    TutorialAttemptResult<TutorialJourneySnapshot> ChangeStatus(
        TutorialStatusCommand command);

    TutorialAttemptResult<FreshTrainingAttemptSnapshot> StartFresh(
        FreshTrainingAttemptCommand command);
}

public abstract record TutorialAttemptResult<T>
{
    private TutorialAttemptResult()
    {
    }

    public sealed record Success(T Value) : TutorialAttemptResult<T>;

    public sealed record Unavailable() : TutorialAttemptResult<T>;

    public sealed record Conflict(string Reason) : TutorialAttemptResult<T>;

    public sealed record Busy() : TutorialAttemptResult<T>;
}

public static class TutorialJourneyEvaluator
{
    public const int FoundationsDefinitionVersion = 1;
    public const string MoveOutcomeAcknowledgement = "foundations.move-outcome";
    public const string BattleOutcomeAcknowledgement = "foundations.battle-outcome";
    public const string ChoiceOutcomeAcknowledgement = "foundations.choice-outcome";

    public static TutorialJourneySnapshot Evaluate(
        GameState state,
        GameCommandContext context,
        TutorialRunSnapshot run,
        IReadOnlySet<string> acknowledgements)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(acknowledgements);

        var cycle = state.Cycles.Single(item => item.CycleId == context.CycleId);
        var ownedFleetIds = state.Fleets
            .Where(item => item.CycleId == cycle.CycleId && item.EmpireId == context.EmpireId)
            .Select(item => item.FleetId)
            .ToHashSet();
        var homeGuard = state.Fleets.SingleOrDefault(item =>
            item.CycleId == cycle.CycleId
            && item.EmpireId == context.EmpireId
            && item.FleetName == "Home Guard");
        var surveyWing = state.Fleets.SingleOrDefault(item =>
            item.CycleId == cycle.CycleId
            && item.EmpireId == context.EmpireId
            && item.FleetName == "Survey Wing");
        var vanguard = state.Fleets.SingleOrDefault(item =>
            item.CycleId == cycle.CycleId
            && item.EmpireId == context.EmpireId
            && item.FleetName == "Vanguard");
        var firstlight = state.Systems.SingleOrDefault(item =>
            item.CycleId == cycle.CycleId && item.SystemName == "Firstlight");
        var greenwater = state.Systems.SingleOrDefault(item =>
            item.CycleId == cycle.CycleId && item.SystemName == "Greenwater");
        var homeGuardEligible = homeGuard is { Status: FleetStatus.Active, ShipCount: > 0 };
        var surveyWingEligible = surveyWing is { Status: FleetStatus.Active, ShipCount: > 0 };
        var vanguardEligible = vanguard is { Status: FleetStatus.Active, ShipCount: > 0 }
                                && state.Fleets.Any(item =>
                                    item.CycleId == cycle.CycleId
                                    && item.CurrentSystemId == vanguard.CurrentSystemId
                                    && item.FactionId != vanguard.FactionId
                                    && item.Status == FleetStatus.Active
                                    && item.ShipCount > 0);

        var moveOrder = homeGuard is null || firstlight is null
            ? null
            : state.FleetOrders
                .Where(item => item.CycleId == cycle.CycleId
                               && item.FleetId == homeGuard.FleetId
                               && item.OrderType == FleetOrderType.MoveFleet
                               && item.TargetSystemId == firstlight.SystemId
                               && item.Status == FleetOrderStatus.Processed
                               && item.CreatedAt >= run.StartedAt)
                .OrderBy(item => item.ProcessedTick)
                .ThenBy(item => item.CreatedAt)
                .FirstOrDefault();
        var moveEvent = moveOrder is null
            ? null
            : state.Events
                .Where(item => item.CycleId == cycle.CycleId
                               && item.EmpireId == context.EmpireId
                               && item.SystemId == firstlight!.SystemId
                               && item.EventType is EventType.FleetMoved or EventType.FleetArrived
                               && item.TickNumber == moveOrder.ProcessedTick)
                .OrderBy(item => item.CreatedAt)
                .FirstOrDefault();
        var pendingMoveOrder = homeGuard is null || firstlight is null
            ? null
            : state.FleetOrders.SingleOrDefault(item =>
                item.CycleId == cycle.CycleId
                && item.FleetId == homeGuard.FleetId
                && item.OrderType == FleetOrderType.MoveFleet
                && item.TargetSystemId == firstlight.SystemId
                && item.Status == FleetOrderStatus.Pending
                && item.ExecuteAfterTick == cycle.CurrentTickNumber + 1
                && item.CreatedAt >= run.StartedAt);
        var t0Evidence = moveOrder is not null && moveEvent is not null;
        var t0Acknowledged = acknowledgements.Contains(MoveOutcomeAcknowledgement);
        var t0Complete = t0Evidence && t0Acknowledged;

        var priorityEvent = state.Events
            .Where(item => item.CycleId == cycle.CycleId
                           && item.EmpireId == context.EmpireId
                           && item.EventType == EventType.PrioritiesChanged
                           && item.CreatedAt >= run.StartedAt
                           && (moveEvent is null || item.CreatedAt >= moveEvent.CreatedAt))
            .OrderBy(item => item.CreatedAt)
            .FirstOrDefault();
        var coloniseOrder = surveyWing is null || greenwater is null
            ? null
            : state.FleetOrders
                .Where(item => item.CycleId == cycle.CycleId
                               && item.FleetId == surveyWing.FleetId
                               && item.OrderType == FleetOrderType.Colonise
                               && item.Status == FleetOrderStatus.Processed
                               && item.ProcessedTick > (moveOrder?.ProcessedTick ?? 0))
                .OrderBy(item => item.ProcessedTick)
                .FirstOrDefault();
        var pendingColoniseOrder = surveyWing is null || greenwater is null
            ? null
            : state.FleetOrders.SingleOrDefault(item =>
                item.CycleId == cycle.CycleId
                && item.FleetId == surveyWing.FleetId
                && item.OrderType == FleetOrderType.Colonise
                && item.TargetSystemId == greenwater.SystemId
                && item.Status == FleetOrderStatus.Pending
                && item.ExecuteAfterTick == cycle.CurrentTickNumber + 1);
        var outpost = greenwater is null
            ? null
            : state.ColonialOutposts
                .Where(item => item.CycleId == cycle.CycleId
                               && item.EmpireId == context.EmpireId
                               && item.SystemId == greenwater.SystemId
                               && item.EstablishedTick == coloniseOrder?.ProcessedTick)
                .OrderBy(item => item.EstablishedTick)
                .FirstOrDefault();
        var t1Evidence = t0Complete && priorityEvent is not null && coloniseOrder is not null && outpost is not null;
        var t1Complete = t1Evidence;

        var attackOrder = vanguard is null
            ? null
            : state.FleetOrders
                .Where(item => item.CycleId == cycle.CycleId
                               && item.FleetId == vanguard.FleetId
                               && item.OrderType == FleetOrderType.Attack
                               && item.Status == FleetOrderStatus.Processed
                               && item.ProcessedTick > (coloniseOrder?.ProcessedTick ?? 0))
                .OrderBy(item => item.ProcessedTick)
                .FirstOrDefault();
        var pendingAttackOrder = vanguard is null
            ? null
            : state.FleetOrders.SingleOrDefault(item =>
                item.CycleId == cycle.CycleId
                && item.FleetId == vanguard.FleetId
                && item.OrderType == FleetOrderType.Attack
                && item.Status == FleetOrderStatus.Pending
                && item.ExecuteAfterTick == cycle.CurrentTickNumber + 1
                && state.Fleets.Any(target =>
                    target.CycleId == cycle.CycleId
                    && target.CurrentSystemId == vanguard.CurrentSystemId
                    && target.FactionId == item.TargetFactionId
                    && target.Status == FleetStatus.Active
                    && target.ShipCount > 0));
        var battle = attackOrder is null
            ? null
            : state.BattleFleetParticipants
                .Where(item => item.CycleId == cycle.CycleId
                               && item.FleetId == attackOrder.FleetId)
                .Join(
                    state.BattleRecords.Where(item => item.CycleId == cycle.CycleId
                                                      && item.TickNumber == attackOrder.ProcessedTick),
                    participant => participant.BattleId,
                    record => record.BattleId,
                    (_, record) => record)
                .OrderBy(item => item.CreatedAt)
                .FirstOrDefault();
        var t2Evidence = t1Complete && attackOrder is not null && battle is not null;
        var t2Acknowledged = acknowledgements.Contains(BattleOutcomeAcknowledgement);
        var t2Complete = t2Evidence && t2Acknowledged;

        var excludedOrders = new[] { moveOrder?.FleetOrderId, coloniseOrder?.FleetOrderId, attackOrder?.FleetOrderId }
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToHashSet();
        var choiceOrder = state.FleetOrders
            .Where(item => item.CycleId == cycle.CycleId
                           && ownedFleetIds.Contains(item.FleetId)
                           && item.CommandSource == FleetOrderCommandSource.Human
                           && item.Status == FleetOrderStatus.Processed
                           && item.ProcessedTick > (battle?.TickNumber ?? int.MaxValue)
                           && !excludedOrders.Contains(item.FleetOrderId))
            .OrderBy(item => item.ProcessedTick)
            .ThenBy(item => item.CreatedAt)
            .FirstOrDefault();
        var pendingChoiceOrder = state.FleetOrders
            .Where(item => item.CycleId == cycle.CycleId
                           && ownedFleetIds.Contains(item.FleetId)
                           && item.CommandSource == FleetOrderCommandSource.Human
                           && item.Status == FleetOrderStatus.Pending
                           && item.ExecuteAfterTick == cycle.CurrentTickNumber + 1)
            .OrderBy(item => item.CreatedAt)
            .FirstOrDefault();
        var t3Evidence = t2Complete && choiceOrder is not null;
        var t3Acknowledged = acknowledgements.Contains(ChoiceOutcomeAcknowledgement);
        var t3Complete = t3Evidence && t3Acknowledged;

        var lessons = new[]
        {
            CreateLesson(
                "T0",
                "Orient and move",
                "Move Home Guard from Hearth to Firstlight, then resolve the Training turn.",
                "Open Home Guard, choose Move, and select the adjacent Firstlight system.",
                entryReady: true,
                resolutionReady: pendingMoveOrder is not null,
                evidenceSatisfied: t0Evidence,
                evidenceSummary: t0Evidence
                    ? "Home Guard's processed Move reached Firstlight."
                    : pendingMoveOrder is not null
                        ? "Home Guard's Move to Firstlight is queued. Resolve the Training turn."
                        : "Queue Home Guard's Move to Firstlight before resolving.",
                factIds: FactIds(moveOrder?.FleetOrderId, moveEvent?.EventId),
                acknowledgementKey: MoveOutcomeAcknowledgement,
                acknowledgementSatisfied: t0Acknowledged,
                blockedReason: !t0Evidence && (!homeGuardEligible || firstlight is null)
                    ? "RequiredStartingPositionUnavailable"
                    : null),
            CreateLesson(
                "T1",
                "Read causality and grow",
                "Save a priority change, colonise Greenwater with Survey Wing, then resolve.",
                "The suggested split is 40% Military and 60% Economy. Colonisation still uses the ordinary population and influence checks.",
                entryReady: t0Complete,
                resolutionReady: priorityEvent is not null && pendingColoniseOrder is not null,
                evidenceSatisfied: t1Evidence,
                evidenceSummary: t1Evidence
                    ? "The priority change and Greenwater outpost are authoritative."
                    : priorityEvent is not null && pendingColoniseOrder is not null
                        ? "Priorities are saved and Survey Wing's Colonise intention is queued. Resolve the Training turn."
                        : priorityEvent is not null
                            ? "Priorities are saved. Queue Survey Wing to colonise Greenwater."
                            : pendingColoniseOrder is not null
                                ? "Survey Wing's Colonise intention is queued. Save a priority change before resolving."
                                : "Save a priority change and queue Survey Wing to colonise Greenwater.",
                factIds: FactIds(priorityEvent?.EventId, coloniseOrder?.FleetOrderId, outpost?.ColonialOutpostId),
                acknowledgementKey: null,
                acknowledgementSatisfied: true,
                blockedReason: t0Complete && !t1Evidence && (!surveyWingEligible || greenwater is null)
                    ? "RequiredColonisationPositionUnavailable"
                    : null),
            CreateLesson(
                "T2",
                "Face uncertainty",
                "Attack the local Drift Corsairs with Vanguard, then resolve.",
                "A real BattleRecord is the goal. Winning is not required.",
                entryReady: t1Complete,
                resolutionReady: pendingAttackOrder is not null,
                evidenceSatisfied: t2Evidence,
                evidenceSummary: t2Evidence
                    ? "Vanguard's processed Attack produced a BattleRecord."
                    : pendingAttackOrder is not null
                        ? "Vanguard's Attack on the local Corsairs is queued. Resolve the Training turn."
                        : "Queue Vanguard to attack the local Corsairs before resolving.",
                factIds: FactIds(attackOrder?.FleetOrderId, battle?.BattleId),
                acknowledgementKey: BattleOutcomeAcknowledgement,
                acknowledgementSatisfied: t2Acknowledged,
                blockedReason: t1Complete && !t2Evidence && !vanguardEligible
                    ? "RequiredCombatPositionUnavailable"
                    : null),
            CreateLesson(
                "T3",
                "Choose a command",
                "Choose any legal command yourself, resolve it, and inspect the real result.",
                "Nothing is preselected in this lesson. Any processed command from one of your fleets counts.",
                entryReady: t2Complete,
                resolutionReady: pendingChoiceOrder is not null,
                evidenceSatisfied: t3Evidence,
                evidenceSummary: t3Evidence
                    ? "Your self-chosen command was processed."
                    : pendingChoiceOrder is not null
                        ? "Your command is queued. Resolve the Training turn."
                        : "Choose and queue any legal command from one of your fleets.",
                factIds: FactIds(choiceOrder?.FleetOrderId),
                acknowledgementKey: ChoiceOutcomeAcknowledgement,
                acknowledgementSatisfied: t3Acknowledged,
                blockedReason: t2Complete && !state.Fleets.Any(item =>
                    item.CycleId == cycle.CycleId
                    && item.EmpireId == context.EmpireId
                    && item.Status == FleetStatus.Active
                    && item.ShipCount > 0)
                    ? "NoEligibleOwnedFleet"
                    : null)
        };

        var coreCompleted = lessons.All(item => item.CompletionState == "Completed");
        var currentLesson = lessons.FirstOrDefault(item => item.CompletionState != "Completed");
        var currentResolutionReady = currentLesson?.Key switch
        {
            "T0" => pendingMoveOrder is not null && !t0Evidence,
            "T1" => priorityEvent is not null && pendingColoniseOrder is not null && !t1Evidence,
            "T2" => pendingAttackOrder is not null && !t2Evidence,
            "T3" => pendingChoiceOrder is not null && !t3Evidence,
            _ => false
        };
        var journeyStatus = cycle.Status == CycleStatus.RecoveryRequired
            ? "RecoveryRequired"
            : run.Status.ToString();
        return new TutorialJourneySnapshot(
            run,
            "Core foundations",
            journeyStatus,
            cycle.CurrentTickNumber,
            currentLesson,
            lessons,
            coreCompleted,
            CanResolve: run.Status == TutorialRunStatus.Active
                        && cycle.Status == CycleStatus.Active
                        && !coreCompleted
                        && currentResolutionReady,
            CanStartFresh: run.Status != TutorialRunStatus.Superseded);
    }

    private static TutorialLessonSnapshot CreateLesson(
        string key,
        string title,
        string objective,
        string hint,
        bool entryReady,
        bool resolutionReady,
        bool evidenceSatisfied,
        string evidenceSummary,
        IReadOnlyList<Guid> factIds,
        string? acknowledgementKey,
        bool acknowledgementSatisfied,
        string? blockedReason)
    {
        var acknowledgementRequired = acknowledgementKey is not null;
        var complete = entryReady
                       && evidenceSatisfied
                       && (!acknowledgementRequired || acknowledgementSatisfied);
        var completionState = complete
            ? "Completed"
            : !entryReady
                ? "Locked"
                : blockedReason is not null
                    ? "Blocked"
                    : evidenceSatisfied && acknowledgementRequired
                        ? "WaitingForAcknowledgement"
                        : "WaitingForEvidence";
        return new TutorialLessonSnapshot(
            key,
            title,
            objective,
            hint,
            !entryReady
                ? "WaitingForPriorLesson"
                : resolutionReady && !evidenceSatisfied
                    ? "WaitingForResolution"
                    : "Ready",
            new TutorialEvidenceSnapshot(evidenceSatisfied, evidenceSummary, factIds),
            new TutorialAcknowledgementSnapshot(
                acknowledgementKey,
                acknowledgementRequired,
                acknowledgementSatisfied),
            completionState,
            blockedReason,
            blockedReason is null ? [] : ["StartFresh"]);
    }

    private static IReadOnlyList<Guid> FactIds(params Guid?[] values) =>
        values.Where(item => item.HasValue).Select(item => item!.Value).ToArray();
}
