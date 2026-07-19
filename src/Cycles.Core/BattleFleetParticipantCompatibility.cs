using System.Text.Json;

namespace Cycles.Core;

public static class BattleFleetParticipantCompatibility
{
    public static void UpgradeLegacyMembership(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        foreach (var battle in state.BattleRecords)
        {
            if (state.BattleFleetParticipants.Any(item => item.BattleId == battle.BattleId))
            {
                continue;
            }

            var attackerFleetIds = ParseLegacyFleetIds(
                battle.AttackerFleetIds,
                $"Battle {battle.BattleId} attacker fleets");
            var defenderFleetIds = ParseLegacyFleetIds(
                battle.DefenderFleetIds,
                $"Battle {battle.BattleId} defender fleets");
            var duplicateFleetId = attackerFleetIds.Intersect(defenderFleetIds).FirstOrDefault();
            if (duplicateFleetId != Guid.Empty)
            {
                throw new InvalidOperationException(
                    $"Battle {battle.BattleId} fleet {duplicateFleetId} appears on both sides.");
            }

            state.BattleFleetParticipants.AddRange(attackerFleetIds.Select(fleetId => new BattleFleetParticipant
            {
                BattleId = battle.BattleId,
                CycleId = battle.CycleId,
                FleetId = fleetId,
                Side = BattleFleetSide.Attacker
            }));
            state.BattleFleetParticipants.AddRange(defenderFleetIds.Select(fleetId => new BattleFleetParticipant
            {
                BattleId = battle.BattleId,
                CycleId = battle.CycleId,
                FleetId = fleetId,
                Side = BattleFleetSide.Defender
            }));
        }
    }

    public static void SynchronizeLegacyFleetIds(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var battleIds = state.BattleRecords.Select(item => item.BattleId).ToHashSet();
        var orphan = state.BattleFleetParticipants.FirstOrDefault(item => !battleIds.Contains(item.BattleId));
        if (orphan is not null)
        {
            throw new InvalidOperationException(
                $"Battle fleet participant for fleet {orphan.FleetId} references absent Battle {orphan.BattleId}.");
        }

        foreach (var battle in state.BattleRecords)
        {
            var participants = state.BattleFleetParticipants
                .Where(item => item.BattleId == battle.BattleId)
                .ToArray();
            var emptyIdentifier = participants.FirstOrDefault(item =>
                item.BattleId == Guid.Empty || item.CycleId == Guid.Empty || item.FleetId == Guid.Empty);
            if (emptyIdentifier is not null)
            {
                throw new InvalidOperationException(
                    $"Battle {battle.BattleId} contains fleet membership with an empty identifier.");
            }
            var wrongCycle = participants.FirstOrDefault(item => item.CycleId != battle.CycleId);
            if (wrongCycle is not null)
            {
                throw new InvalidOperationException(
                    $"Battle {battle.BattleId} and fleet {wrongCycle.FleetId} membership belong to different Cycles.");
            }

            var duplicate = participants
                .GroupBy(item => item.FleetId)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate is not null)
            {
                throw new InvalidOperationException(
                    $"Battle {battle.BattleId} contains duplicate fleet membership {duplicate.Key}.");
            }

            var invalidSide = participants.FirstOrDefault(item => !Enum.IsDefined(item.Side));
            if (invalidSide is not null)
            {
                throw new InvalidOperationException(
                    $"Battle {battle.BattleId} fleet {invalidSide.FleetId} has invalid side '{invalidSide.Side}'.");
            }

            var attackerFleetIds = participants
                .Where(item => item.Side == BattleFleetSide.Attacker)
                .Select(item => item.FleetId)
                .Order()
                .ToArray();
            var defenderFleetIds = participants
                .Where(item => item.Side == BattleFleetSide.Defender)
                .Select(item => item.FleetId)
                .Order()
                .ToArray();
            if (attackerFleetIds.Length == 0 || defenderFleetIds.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Battle {battle.BattleId} must retain at least one fleet on each side.");
            }

            var legacyAttackerFleetIds = ParseLegacyFleetIds(
                battle.AttackerFleetIds,
                $"Battle {battle.BattleId} attacker fleets");
            var legacyDefenderFleetIds = ParseLegacyFleetIds(
                battle.DefenderFleetIds,
                $"Battle {battle.BattleId} defender fleets");
            if (!attackerFleetIds.ToHashSet().SetEquals(legacyAttackerFleetIds)
                || !defenderFleetIds.ToHashSet().SetEquals(legacyDefenderFleetIds))
            {
                throw new InvalidOperationException(
                    $"Battle {battle.BattleId} normalized fleet membership does not match its retained legacy lists.");
            }

            battle.AttackerFleetIds = FormatLegacyFleetIds(attackerFleetIds);
            battle.DefenderFleetIds = FormatLegacyFleetIds(defenderFleetIds);
        }
    }

    public static IReadOnlyList<Guid> ParseLegacyFleetIds(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{label} is empty.");
        }

        var trimmed = value.Trim();
        IReadOnlyList<string> tokens;
        if (trimmed[0] == '[')
        {
            tokens = ParseJsonArray(trimmed, label);
        }
        else
        {
            tokens = trimmed.Split(',', StringSplitOptions.TrimEntries);
        }

        if (tokens.Count == 0)
        {
            throw new InvalidOperationException($"{label} is empty.");
        }

        var fleetIds = new List<Guid>(tokens.Count);
        var seen = new HashSet<Guid>();
        foreach (var token in tokens)
        {
            var trimmedToken = token.Trim();
            if (trimmedToken.Length == 0
                || !Guid.TryParseExact(trimmedToken, "D", out var fleetId)
                || fleetId == Guid.Empty)
            {
                throw new InvalidOperationException($"{label} contains invalid fleet identifier '{token}'.");
            }

            if (!seen.Add(fleetId))
            {
                throw new InvalidOperationException($"{label} contains duplicate fleet identifier {fleetId}.");
            }

            fleetIds.Add(fleetId);
        }

        return fleetIds;
    }

    public static string FormatLegacyFleetIds(IEnumerable<Guid> fleetIds) =>
        string.Join(",", fleetIds.Order().Select(item => item.ToString("D")));

    private static IReadOnlyList<string> ParseJsonArray(string value, string label)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(value);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"{label} contains invalid JSON: {exception.Message}", exception);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException($"{label} must contain a JSON array when JSON syntax is used.");
            }

            var tokens = new List<string>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidOperationException($"{label} JSON values must be fleet identifier strings.");
                }

                tokens.Add(item.GetString() ?? "");
            }

            return tokens;
        }
    }
}
