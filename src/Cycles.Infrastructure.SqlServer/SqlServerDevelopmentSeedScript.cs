using Cycles.Core;
using System.Globalization;
using System.Text;

namespace Cycles.Infrastructure.SqlServer;

public static class SqlServerDevelopmentSeedScript
{
    public static readonly DateTimeOffset CanonicalCreatedAt = new(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);

    public static string Generate()
    {
        var state = GameSeeder.CreateCuratedColdStart(CanonicalCreatedAt);
        EnsureSupportedSeedShape(state);

        var script = new StringBuilder();
        script.AppendLine("-- Generated from GameSeeder.CreateCuratedColdStart. Do not hand-edit.");
        script.AppendLine("SET ANSI_NULLS ON;");
        script.AppendLine("SET QUOTED_IDENTIFIER ON;");
        script.AppendLine("SET ANSI_PADDING ON;");
        script.AppendLine("SET ANSI_WARNINGS ON;");
        script.AppendLine("SET CONCAT_NULL_YIELDS_NULL ON;");
        script.AppendLine("SET ARITHABORT ON;");
        script.AppendLine("SET NUMERIC_ROUNDABORT OFF;");
        script.AppendLine("SET XACT_ABORT ON;");
        script.AppendLine();
        script.AppendLine("BEGIN TRANSACTION;");
        script.AppendLine("IF NOT EXISTS (SELECT 1 FROM dbo.Cycles)");
        script.AppendLine("BEGIN");
        script.AppendLine("    DECLARE @SeededAt DATETIMEOFFSET = SYSDATETIMEOFFSET();");
        script.AppendLine("    DECLARE @CycleName NVARCHAR(120) = CONCAT(N'Cycle ', DATEPART(YEAR, @SeededAt), N'.', RIGHT(N'0' + CONVERT(NVARCHAR(2), DATEPART(MONTH, @SeededAt)), 2));");
        script.AppendLine();

        AppendInsert(script, "dbo.Players", "PlayerID, Username, Email, PasswordHash, ExternalIssuer, ExternalSubject, PlayerKind, Role, CreatedAt, LastLoginAt, Status", state.Players, item =>
            $"({Sql(item.PlayerId)}, {Sql(item.Username)}, {Sql(item.Email)}, {Sql(item.PasswordHash)}, {Sql(item.ExternalIssuer)}, {Sql(item.ExternalSubject)}, {Sql(item.Kind)}, {Sql(item.Role)}, {SqlSeedTime(item.CreatedAt)}, {SqlSeedTime(item.LastLoginAt)}, {Sql(item.Status)})");
        AppendInsert(script, "dbo.Games", "GameID, Name, Purpose, Status, Visibility, CreationSource, GamePolicyKey, GamePolicyVersion, GamePolicyContentHash, PolicyProvenanceStatus, CreatedByPlayerID, CreatedAt, FirstStartedAt, CompletedAt, CancelledAt, TerminatedAt", state.Games, item =>
            $"({Sql(item.GameId)}, {Sql(item.Name)}, {Sql(item.Purpose)}, {Sql(item.Status)}, {Sql(item.Visibility)}, {Sql(item.CreationSource)}, {Sql(item.GamePolicyKey)}, {item.GamePolicyVersion}, {Sql(item.GamePolicyContentHash)}, {Sql(item.PolicyProvenanceStatus)}, {Sql(item.CreatedByPlayerId)}, {SqlSeedTime(item.CreatedAt)}, {SqlSeedTime(item.FirstStartedAt)}, {SqlSeedTime(item.CompletedAt)}, {SqlSeedTime(item.CancelledAt)}, {SqlSeedTime(item.TerminatedAt)})");
        AppendInsert(script, "dbo.CycleConfigurations", "CycleConfigurationID, GameID, SequenceNumber, Status, ProvenanceStatus, MapProfileKey, MapProfileVersion, MapProfileContentHash, MapSeed, ScenarioProfileKey, ScenarioProfileVersion, ScenarioProfileContentHash, ScenarioSeed, CyclePolicyKey, CyclePolicyVersion, CyclePolicyContentHash, MinimumHumanSeats, MaximumHumanSeats, ScheduledStartAt, ScheduledEndAt, TickLengthMinutes, CreatedAt, LockedAt, MaterializedAt, CancelledAt", state.CycleConfigurations.OrderBy(item => item.SequenceNumber), item =>
            $"({Sql(item.CycleConfigurationId)}, {Sql(item.GameId)}, {item.SequenceNumber}, {Sql(item.Status)}, {Sql(item.ProvenanceStatus)}, {Sql(item.MapProfileKey)}, {Sql(item.MapProfileVersion)}, {Sql(item.MapProfileContentHash)}, {Sql(item.MapSeed)}, {Sql(item.ScenarioProfileKey)}, {Sql(item.ScenarioProfileVersion)}, {Sql(item.ScenarioProfileContentHash)}, {Sql(item.ScenarioSeed)}, {Sql(item.CyclePolicyKey)}, {item.CyclePolicyVersion}, {Sql(item.CyclePolicyContentHash)}, {Sql(item.MinimumHumanSeats)}, {Sql(item.MaximumHumanSeats)}, {SqlSeedTime(item.ScheduledStartAt)}, {SqlSeedTime(item.ScheduledEndAt)}, {Sql(item.TickLengthMinutes)}, {SqlSeedTime(item.CreatedAt)}, {SqlSeedTime(item.LockedAt)}, {SqlSeedTime(item.MaterializedAt)}, {SqlSeedTime(item.CancelledAt)})");
        AppendInsert(script, "dbo.Cycles", "CycleID, GameID, CycleConfigurationID, PreviousCycleID, Name, StartAt, EndAt, TickLengthMinutes, CurrentTickNumber, Status, TurnStage, MapProfileKey, MapProfileVersion, MapProfileContentHash, MapSeed, ScenarioProfileKey, ScenarioProfileVersion, ScenarioProfileContentHash, ScenarioSeed, CyclePolicyKey, CyclePolicyVersion, CyclePolicyContentHash, ProfileProvenanceStatus, CreatedByPlayerID, CreatedAt", state.Cycles, item =>
            $"({Sql(item.CycleId)}, {Sql(item.GameId)}, {Sql(item.CycleConfigurationId)}, {Sql(item.PreviousCycleId)}, @CycleName, {SqlSeedTime(item.StartAt)}, {SqlSeedTime(item.EndAt)}, {item.TickLengthMinutes}, {item.CurrentTickNumber}, {Sql(item.Status)}, {Sql(item.TurnStage)}, {Sql(item.MapProfileKey)}, {Sql(item.MapProfileVersion)}, {Sql(item.MapProfileContentHash)}, {Sql(item.MapSeed)}, {Sql(item.ScenarioProfileKey)}, {Sql(item.ScenarioProfileVersion)}, {Sql(item.ScenarioProfileContentHash)}, {Sql(item.ScenarioSeed)}, {Sql(item.CyclePolicyKey)}, {Sql(item.CyclePolicyVersion)}, {Sql(item.CyclePolicyContentHash)}, {Sql(item.ProfileProvenanceStatus)}, {Sql(item.CreatedByPlayerId)}, {SqlSeedTime(item.CreatedAt)})");
        AppendInsert(script, "dbo.GameEnrolments", "GameEnrolmentID, GameID, PlayerID, Status, Origin, OriginatingRequestID, EnrolledAt, StatusChangedAt, EndedAt", state.GameEnrolments.OrderBy(item => item.PlayerId), item =>
            $"({Sql(item.GameEnrolmentId)}, {Sql(item.GameId)}, {Sql(item.PlayerId)}, {Sql(item.Status)}, {Sql(item.Origin)}, {Sql(item.OriginatingRequestId)}, {SqlSeedTime(item.EnrolledAt)}, {SqlSeedTime(item.StatusChangedAt)}, {SqlSeedTime(item.EndedAt)})");
        AppendInsert(script, "dbo.GameLifecycleEvents", "GameLifecycleEventID, GameID, EventType, SubjectPlayerID, ActorPlayerID, FromStatus, ToStatus, Reason, CorrelationID, FactJson, CreatedAt", state.GameLifecycleEvents.OrderBy(item => item.CreatedAt), item =>
            $"({Sql(item.GameLifecycleEventId)}, {Sql(item.GameId)}, {Sql(item.Type)}, {Sql(item.SubjectPlayerId)}, {Sql(item.ActorPlayerId)}, {Sql(item.FromStatus)}, {Sql(item.ToStatus)}, {Sql(item.Reason)}, {Sql(item.CorrelationId)}, {Sql(item.FactJson)}, {SqlSeedTime(item.CreatedAt)})");
        AppendInsert(script, "dbo.GalaxySectors", "SectorID, CycleID, SectorName, CentreX, CentreY, SortOrder", state.Sectors.OrderBy(item => item.SortOrder), item =>
            $"({Sql(item.SectorId)}, {Sql(item.CycleId)}, {Sql(item.SectorName)}, {item.CentreX}, {item.CentreY}, {item.SortOrder})");
        AppendInsert(script, "dbo.Systems", "SystemID, CycleID, SectorID, SystemName, X, Y, IndustryOutput, ResearchOutput, PopulationOutput, StrategicValue, HistoricalSignificance, CreatedAt", state.Systems.OrderBy(item => item.SystemName), item =>
            $"({Sql(item.SystemId)}, {Sql(item.CycleId)}, {Sql(item.SectorId)}, {Sql(item.SystemName)}, {item.X}, {item.Y}, {Sql(item.IndustryOutput)}, {Sql(item.ResearchOutput)}, {Sql(item.PopulationOutput)}, {item.StrategicValue}, {item.HistoricalSignificance}, {SqlSeedTime(item.CreatedAt)})");
        AppendInsert(script, "dbo.Empires", "EmpireID, CycleID, PlayerID, EmpireName, HomeSystemID, CreatedAt, Status", state.Empires.OrderBy(item => item.EmpireName), item =>
            $"({Sql(item.EmpireId)}, {Sql(item.CycleId)}, {Sql(item.PlayerId)}, {Sql(item.EmpireName)}, {Sql(item.HomeSystemId)}, {SqlSeedTime(item.CreatedAt)}, {Sql(item.Status)})");
        AppendInsert(script, "dbo.Factions", "FactionID, CycleID, EmpireID, FactionName, Kind, Status, CreatedAt", state.Factions.OrderBy(item => item.FactionName), item =>
            $"({Sql(item.FactionId)}, {Sql(item.CycleId)}, {Sql(item.EmpireId)}, {Sql(item.FactionName)}, {Sql(item.Kind)}, {Sql(item.Status)}, {SqlSeedTime(item.CreatedAt)})");
        AppendInsert(script, "dbo.MatchParticipants", "MatchParticipantID, CycleID, PlayerID, EmpireID, Status, JoinedAt, EndedAt", state.MatchParticipants.OrderBy(item => item.PlayerId), item =>
            $"({Sql(item.MatchParticipantId)}, {Sql(item.CycleId)}, {Sql(item.PlayerId)}, {Sql(item.EmpireId)}, {Sql(item.Status)}, {SqlSeedTime(item.JoinedAt)}, {SqlSeedTime(item.EndedAt)})");
        AppendInsert(script, "dbo.EmpireResources", "EmpireResourceID, EmpireID, Industry, Research, Population, LastGeneratedIndustry, LastGeneratedResearch, LastGeneratedPopulation, LastSpentIndustry, LastSpentResearch, LastSpentPopulation, UpdatedAt", state.EmpireResources, item =>
            $"({Sql(item.EmpireResourceId)}, {Sql(item.EmpireId)}, {Sql(item.Industry)}, {Sql(item.Research)}, {Sql(item.Population)}, {Sql(item.LastGeneratedIndustry)}, {Sql(item.LastGeneratedResearch)}, {Sql(item.LastGeneratedPopulation)}, {Sql(item.LastSpentIndustry)}, {Sql(item.LastSpentResearch)}, {Sql(item.LastSpentPopulation)}, {SqlSeedTime(item.UpdatedAt)})");
        AppendInsert(script, "dbo.EmpirePriorities", "EmpirePriorityID, EmpireID, IndustryWeight, ResearchWeight, MilitaryWeight, ExpansionWeight, UpdatedAt", state.EmpirePriorities, item =>
            $"({Sql(item.EmpirePriorityId)}, {Sql(item.EmpireId)}, {item.IndustryWeight}, {item.ResearchWeight}, {item.MilitaryWeight}, {item.ExpansionWeight}, {SqlSeedTime(item.UpdatedAt)})");
        AppendInsert(script, "dbo.SystemLinks", "SystemLinkID, CycleID, SystemAID, SystemBID, Distance, TravelTicks", state.SystemLinks.OrderBy(item => item.SystemLinkId), item =>
            $"({Sql(item.SystemLinkId)}, {Sql(item.CycleId)}, {Sql(item.SystemAId)}, {Sql(item.SystemBId)}, {Sql(item.Distance)}, {item.TravelTicks})");
        AppendInsert(script, "dbo.Admirals", "AdmiralID, CycleID, EmpireID, AdmiralName, ReputationScore, Status, CreatedAt, UpdatedAt", state.Admirals.OrderBy(item => item.AdmiralName), item =>
            $"({Sql(item.AdmiralId)}, {Sql(item.CycleId)}, {Sql(item.EmpireId)}, {Sql(item.AdmiralName)}, {item.ReputationScore}, {Sql(item.Status)}, {SqlSeedTime(item.CreatedAt)}, {SqlSeedTime(item.UpdatedAt)})");
        AppendInsert(script, "dbo.Fleets", "FleetID, CycleID, EmpireID, FactionID, AdmiralID, FleetName, CurrentSystemID, DestinationSystemID, DepartureTickNumber, ArrivalTickNumber, ShipCount, Status, CreatedAt", state.Fleets.OrderBy(item => item.FleetName), item =>
            $"({Sql(item.FleetId)}, {Sql(item.CycleId)}, {Sql(item.EmpireId == Guid.Empty ? (Guid?)null : item.EmpireId)}, {Sql(item.FactionId)}, {Sql(item.AdmiralId)}, {Sql(item.FleetName)}, {Sql(item.CurrentSystemId)}, {Sql(item.DestinationSystemId)}, {Sql(item.DepartureTickNumber)}, {Sql(item.ArrivalTickNumber)}, {item.ShipCount}, {Sql(item.Status)}, {SqlSeedTime(item.CreatedAt)})");
        AppendInsert(script, "dbo.FleetOrders", "FleetOrderID, CycleID, FleetID, OrderType, TargetSystemID, TargetEmpireID, TargetFactionID, SubmitTick, ExecuteAfterTick, ProcessedTick, Status, RejectionReason, SupersededByOrderID, CreatedAt", state.FleetOrders.OrderBy(item => item.FleetOrderId), item =>
            $"({Sql(item.FleetOrderId)}, {Sql(item.CycleId)}, {Sql(item.FleetId)}, {Sql(item.OrderType)}, {Sql(item.TargetSystemId)}, {Sql(item.TargetEmpireId)}, {Sql(item.TargetFactionId)}, {item.SubmitTick}, {item.ExecuteAfterTick}, {Sql(item.ProcessedTick)}, {Sql(item.Status)}, {Sql(item.RejectionReason)}, {Sql(item.SupersededByOrderId)}, {SqlSeedTime(item.CreatedAt)})");
        AppendInsert(script, "dbo.Events", "EventID, CycleID, TickNumber, EventType, SystemID, EmpireID, FactionID, Severity, FactJson, DisplayText, CreatedAt", state.Events.OrderBy(item => item.EventId), item =>
            $"({Sql(item.EventId)}, {Sql(item.CycleId)}, {item.TickNumber}, {Sql(item.EventType)}, {Sql(item.SystemId)}, {Sql(item.EmpireId)}, {Sql(item.FactionId)}, {Sql(item.Severity)}, {Sql(item.FactJson)}, {SqlSeedDisplayText(item)}, {SqlSeedTime(item.CreatedAt)})");

        script.AppendLine("END;");
        script.AppendLine("COMMIT TRANSACTION;");
        return script.ToString();
    }

    private static void EnsureSupportedSeedShape(GameState state)
    {
        var sectorIds = state.Sectors.Select(item => item.SectorId).ToHashSet();
        if (sectorIds.Count == 0
            || state.Systems.Any(item => item.SectorId == Guid.Empty || !sectorIds.Contains(item.SectorId)))
        {
            throw new InvalidOperationException("The curated cold start must assign every system to a persisted galaxy sector.");
        }

        if (state.Games.Count != 1
            || state.CycleConfigurations.Count != state.Cycles.Count
            || state.GameLifecycleEvents.Count != 1
            || state.Cycles.Any(cycle => cycle.GameId is null || cycle.CycleConfigurationId is null)
            || state.GameEnrolments.Count != state.MatchParticipants.Select(item => item.PlayerId).Distinct().Count())
        {
            throw new InvalidOperationException("The curated cold start must contain one complete legacy Game foundation.");
        }

        var unsupportedRecords = state.AdminRoleAuditRecords.Count
                                 + state.EmpireMetrics.Count
                                 + state.CycleRankings.Count
                                 + state.CycleMajorEvents.Count
                                 + state.SystemHistoricalSignals.Count
                                 + state.ColonialOutposts.Count
                                 + state.DiplomaticRelationships.Count
                                 + state.AdmiralBattleHistories.Count
                                 + state.ShipConstructions.Count
                                 + state.TickLogs.Count
                                 + state.BattleRecords.Count
                                 + state.ChronicleEntries.Count;
        if (unsupportedRecords != 0)
        {
            throw new InvalidOperationException("The curated cold start now contains records that the SQL development seed generator does not map.");
        }
    }

    private static void AppendInsert<T>(
        StringBuilder script,
        string table,
        string columns,
        IEnumerable<T> source,
        Func<T, string> formatRow)
    {
        var rows = source.Select(formatRow).ToArray();
        if (rows.Length == 0)
        {
            return;
        }

        const int maximumRowsPerInsert = 500;
        for (var offset = 0; offset < rows.Length; offset += maximumRowsPerInsert)
        {
            var count = Math.Min(maximumRowsPerInsert, rows.Length - offset);
            script.Append("    INSERT INTO ").Append(table).Append('(').Append(columns).AppendLine(")");
            script.AppendLine("    VALUES");
            for (var index = 0; index < count; index++)
            {
                script.Append("        ").Append(rows[offset + index]).AppendLine(index == count - 1 ? ";" : ",");
            }

            script.AppendLine();
        }
    }

    private static string Sql(Guid value) => $"'{value:D}'";
    private static string Sql(Guid? value) => value.HasValue ? Sql(value.Value) : "NULL";
    private static string Sql(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "NULL";
    private static string Sql(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static string SqlSeedTime(DateTimeOffset value)
    {
        var offset = value - CanonicalCreatedAt;
        if (offset == TimeSpan.Zero)
        {
            return "@SeededAt";
        }

        if (offset.TotalDays == Math.Truncate(offset.TotalDays))
        {
            return $"DATEADD(DAY, {offset.TotalDays.ToString(CultureInfo.InvariantCulture)}, @SeededAt)";
        }

        throw new InvalidOperationException($"The curated seed contains an unsupported relative timestamp: {value:O}.");
    }

    private static string SqlSeedTime(DateTimeOffset? value) => value.HasValue ? SqlSeedTime(value.Value) : "NULL";

    private static string SqlSeedDisplayText(EventRecord item)
    {
        if (item.EventType != EventType.CycleSeeded)
        {
            return Sql(item.DisplayText);
        }

        var suffixIndex = item.DisplayText.IndexOf(" began", StringComparison.Ordinal);
        if (suffixIndex < 0)
        {
            throw new InvalidOperationException("The curated CycleSeeded event no longer contains the expected cycle-name boundary.");
        }

        return $"N'The ' + @CycleName + {Sql(item.DisplayText[suffixIndex..])}";
    }

    private static string Sql(DateTimeOffset value) => $"CAST('{value:O}' AS DATETIMEOFFSET)";
    private static string Sql(DateTimeOffset? value) => value.HasValue ? Sql(value.Value) : "NULL";
    private static string Sql<TEnum>(TEnum value) where TEnum : struct, Enum => Sql(value.ToString());
    private static string Sql<TEnum>(TEnum? value) where TEnum : struct, Enum => value.HasValue ? Sql(value.Value) : "NULL";
    private static string Sql(string? value) => value is null ? "NULL" : $"N'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}
