using System.Text.Json;
using Cycles.Core;

public static class OpeningBriefingContract
{
    public static OpeningBriefingResponse? FindVisible(
        GameState state,
        Cycle cycle,
        DevelopmentActor actor,
        IReadOnlySet<Guid> visibleSystemIds)
    {
        var source = state.Events
            .Where(item => item.CycleId == cycle.CycleId
                           && item.EventType == EventType.OpeningBriefingIssued
                           && item.EmpireId == actor.Empire?.EmpireId)
            .Where(item => ApiVisibility.CanSeeEvent(item, actor, visibleSystemIds))
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();
        if (source is null)
        {
            return null;
        }

        try
        {
            var response = JsonSerializer.Deserialize<OpeningBriefingResponse>(source.FactJson, GameStateJson.Options);
            return IsComplete(response) ? response : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsComplete(OpeningBriefingResponse? response) =>
        response is not null
        && !string.IsNullOrWhiteSpace(response.ScenarioKey)
        && response.FocusSystemId != Guid.Empty
        && response.Objectives.Move.FleetId != Guid.Empty
        && response.Objectives.Move.TargetSystemId != Guid.Empty
        && response.Objectives.Colonise.FleetId != Guid.Empty
        && response.Objectives.Colonise.SystemId != Guid.Empty
        && response.Objectives.Attack.FleetId != Guid.Empty
        && response.Objectives.Attack.SystemId != Guid.Empty
        && ((response.Objectives.Attack.TargetFactionId.HasValue && response.Objectives.Attack.TargetFactionId.Value != Guid.Empty)
            || (response.Objectives.Attack.TargetEmpireId.HasValue && response.Objectives.Attack.TargetEmpireId.Value != Guid.Empty));
}

public sealed record OpeningBriefingResponse(
    string ScenarioKey,
    Guid FocusSystemId,
    OpeningBriefingObjectivesResponse Objectives);

public sealed record OpeningBriefingObjectivesResponse(
    OpeningMoveObjectiveResponse Move,
    OpeningColoniseObjectiveResponse Colonise,
    OpeningAttackObjectiveResponse Attack);

public sealed record OpeningMoveObjectiveResponse(Guid FleetId, Guid TargetSystemId);

public sealed record OpeningColoniseObjectiveResponse(Guid FleetId, Guid SystemId);

public sealed record OpeningAttackObjectiveResponse(
    Guid FleetId,
    Guid SystemId,
    Guid? TargetEmpireId = null,
    Guid? TargetFactionId = null);
