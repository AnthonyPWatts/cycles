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

        AppendInsert(script, "dbo.Players", "PlayerID, Username, Email, PasswordHash, ExternalIssuer, ExternalSubject, Role, CreatedAt, LastLoginAt, Status", state.Players, item =>
            $"({Sql(item.PlayerId)}, {Sql(item.Username)}, {Sql(item.Email)}, {Sql(item.PasswordHash)}, {Sql(item.ExternalIssuer)}, {Sql(item.ExternalSubject)}, {Sql(item.Role)}, {SqlSeedTime(item.CreatedAt)}, {SqlSeedTime(item.LastLoginAt)}, {Sql(item.Status)})");
        AppendInsert(script, "dbo.Cycles", "CycleID, Name, StartAt, EndAt, TickLengthMinutes, CurrentTickNumber, Status, CreatedAt", state.Cycles, item =>
            $"({Sql(item.CycleId)}, @CycleName, {SqlSeedTime(item.StartAt)}, {SqlSeedTime(item.EndAt)}, {item.TickLengthMinutes}, {item.CurrentTickNumber}, {Sql(item.Status)}, {SqlSeedTime(item.CreatedAt)})");
        AppendInsert(script, "dbo.GalaxySectors", "SectorID, CycleID, SectorName, CentreX, CentreY, SortOrder", state.Sectors.OrderBy(item => item.SortOrder), item =>
            $"({Sql(item.SectorId)}, {Sql(item.CycleId)}, {Sql(item.SectorName)}, {item.CentreX}, {item.CentreY}, {item.SortOrder})");
        AppendInsert(script, "dbo.Systems", "SystemID, CycleID, SectorID, SystemName, X, Y, IndustryOutput, ResearchOutput, PopulationOutput, StrategicValue, HistoricalSignificance, CreatedAt", state.Systems.OrderBy(item => item.SystemName), item =>
            $"({Sql(item.SystemId)}, {Sql(item.CycleId)}, {Sql(item.SectorId)}, {Sql(item.SystemName)}, {item.X}, {item.Y}, {Sql(item.IndustryOutput)}, {Sql(item.ResearchOutput)}, {Sql(item.PopulationOutput)}, {item.StrategicValue}, {item.HistoricalSignificance}, {SqlSeedTime(item.CreatedAt)})");
        AppendInsert(script, "dbo.Empires", "EmpireID, CycleID, PlayerID, EmpireName, HomeSystemID, CreatedAt, Status", state.Empires.OrderBy(item => item.EmpireName), item =>
            $"({Sql(item.EmpireId)}, {Sql(item.CycleId)}, {Sql(item.PlayerId)}, {Sql(item.EmpireName)}, {Sql(item.HomeSystemId)}, {SqlSeedTime(item.CreatedAt)}, {Sql(item.Status)})");
        AppendInsert(script, "dbo.EmpireResources", "EmpireResourceID, EmpireID, Industry, Research, Population, LastGeneratedIndustry, LastGeneratedResearch, LastGeneratedPopulation, LastSpentIndustry, LastSpentResearch, LastSpentPopulation, UpdatedAt", state.EmpireResources, item =>
            $"({Sql(item.EmpireResourceId)}, {Sql(item.EmpireId)}, {Sql(item.Industry)}, {Sql(item.Research)}, {Sql(item.Population)}, {Sql(item.LastGeneratedIndustry)}, {Sql(item.LastGeneratedResearch)}, {Sql(item.LastGeneratedPopulation)}, {Sql(item.LastSpentIndustry)}, {Sql(item.LastSpentResearch)}, {Sql(item.LastSpentPopulation)}, {SqlSeedTime(item.UpdatedAt)})");
        AppendInsert(script, "dbo.EmpirePriorities", "EmpirePriorityID, EmpireID, IndustryWeight, ResearchWeight, MilitaryWeight, ExpansionWeight, UpdatedAt", state.EmpirePriorities, item =>
            $"({Sql(item.EmpirePriorityId)}, {Sql(item.EmpireId)}, {item.IndustryWeight}, {item.ResearchWeight}, {item.MilitaryWeight}, {item.ExpansionWeight}, {SqlSeedTime(item.UpdatedAt)})");
        AppendInsert(script, "dbo.SystemLinks", "SystemLinkID, CycleID, SystemAID, SystemBID, Distance, TravelTicks", state.SystemLinks.OrderBy(item => item.SystemLinkId), item =>
            $"({Sql(item.SystemLinkId)}, {Sql(item.CycleId)}, {Sql(item.SystemAId)}, {Sql(item.SystemBId)}, {Sql(item.Distance)}, {item.TravelTicks})");
        AppendInsert(script, "dbo.Admirals", "AdmiralID, CycleID, EmpireID, AdmiralName, ReputationScore, Status, CreatedAt, UpdatedAt", state.Admirals.OrderBy(item => item.AdmiralName), item =>
            $"({Sql(item.AdmiralId)}, {Sql(item.CycleId)}, {Sql(item.EmpireId)}, {Sql(item.AdmiralName)}, {item.ReputationScore}, {Sql(item.Status)}, {SqlSeedTime(item.CreatedAt)}, {SqlSeedTime(item.UpdatedAt)})");
        AppendInsert(script, "dbo.Fleets", "FleetID, CycleID, EmpireID, AdmiralID, FleetName, CurrentSystemID, DestinationSystemID, ArrivalTickNumber, ShipCount, Status, CreatedAt", state.Fleets.OrderBy(item => item.FleetName), item =>
            $"({Sql(item.FleetId)}, {Sql(item.CycleId)}, {Sql(item.EmpireId)}, {Sql(item.AdmiralId)}, {Sql(item.FleetName)}, {Sql(item.CurrentSystemId)}, {Sql(item.DestinationSystemId)}, {Sql(item.ArrivalTickNumber)}, {item.ShipCount}, {Sql(item.Status)}, {SqlSeedTime(item.CreatedAt)})");
        AppendInsert(script, "dbo.FleetOrders", "FleetOrderID, CycleID, FleetID, OrderType, TargetSystemID, TargetEmpireID, SubmitTick, ExecuteAfterTick, ProcessedTick, Status, RejectionReason, SupersededByOrderID, CreatedAt", state.FleetOrders.OrderBy(item => item.FleetOrderId), item =>
            $"({Sql(item.FleetOrderId)}, {Sql(item.CycleId)}, {Sql(item.FleetId)}, {Sql(item.OrderType)}, {Sql(item.TargetSystemId)}, {Sql(item.TargetEmpireId)}, {item.SubmitTick}, {item.ExecuteAfterTick}, {Sql(item.ProcessedTick)}, {Sql(item.Status)}, {Sql(item.RejectionReason)}, {Sql(item.SupersededByOrderId)}, {SqlSeedTime(item.CreatedAt)})");
        AppendInsert(script, "dbo.Events", "EventID, CycleID, TickNumber, EventType, SystemID, EmpireID, Severity, FactJson, DisplayText, CreatedAt", state.Events.OrderBy(item => item.EventId), item =>
            $"({Sql(item.EventId)}, {Sql(item.CycleId)}, {item.TickNumber}, {Sql(item.EventType)}, {Sql(item.SystemId)}, {Sql(item.EmpireId)}, {Sql(item.Severity)}, {Sql(item.FactJson)}, {SqlSeedDisplayText(item)}, {SqlSeedTime(item.CreatedAt)})");

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
    private static string Sql(string? value) => value is null ? "NULL" : $"N'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}
