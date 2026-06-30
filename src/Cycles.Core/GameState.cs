namespace Cycles.Core;

public sealed class GameState
{
    public List<Player> Players { get; set; } = [];
    public List<Cycle> Cycles { get; set; } = [];
    public List<Empire> Empires { get; set; } = [];
    public List<EmpireResource> EmpireResources { get; set; } = [];
    public List<EmpirePriority> EmpirePriorities { get; set; } = [];
    public List<EmpireMetric> EmpireMetrics { get; set; } = [];
    public List<CycleRanking> CycleRankings { get; set; } = [];
    public List<CycleMajorEvent> CycleMajorEvents { get; set; } = [];
    public List<SystemHistoricalSignal> SystemHistoricalSignals { get; set; } = [];
    public List<Admiral> Admirals { get; set; } = [];
    public List<AdmiralBattleHistory> AdmiralBattleHistories { get; set; } = [];
    public List<GalaxySystem> Systems { get; set; } = [];
    public List<SystemLink> SystemLinks { get; set; } = [];
    public List<Fleet> Fleets { get; set; } = [];
    public List<FleetOrder> FleetOrders { get; set; } = [];
    public List<ShipConstruction> ShipConstructions { get; set; } = [];
    public List<TickLog> TickLogs { get; set; } = [];
    public List<EventRecord> Events { get; set; } = [];
    public List<BattleRecord> BattleRecords { get; set; } = [];
    public List<ChronicleEntry> ChronicleEntries { get; set; } = [];

    public Cycle? GetActiveCycle() =>
        Cycles
            .Where(cycle => cycle.Status == CycleStatus.Active)
            .OrderByDescending(cycle => cycle.StartAt)
            .FirstOrDefault();

    public GameState DeepClone() =>
        new()
        {
            Players = Players.Select(Clone).ToList(),
            Cycles = Cycles.Select(Clone).ToList(),
            Empires = Empires.Select(Clone).ToList(),
            EmpireResources = EmpireResources.Select(Clone).ToList(),
            EmpirePriorities = EmpirePriorities.Select(Clone).ToList(),
            EmpireMetrics = EmpireMetrics.Select(Clone).ToList(),
            CycleRankings = CycleRankings.Select(Clone).ToList(),
            CycleMajorEvents = CycleMajorEvents.Select(Clone).ToList(),
            SystemHistoricalSignals = SystemHistoricalSignals.Select(Clone).ToList(),
            Admirals = Admirals.Select(Clone).ToList(),
            AdmiralBattleHistories = AdmiralBattleHistories.Select(Clone).ToList(),
            Systems = Systems.Select(Clone).ToList(),
            SystemLinks = SystemLinks.Select(Clone).ToList(),
            Fleets = Fleets.Select(Clone).ToList(),
            FleetOrders = FleetOrders.Select(Clone).ToList(),
            ShipConstructions = ShipConstructions.Select(Clone).ToList(),
            TickLogs = TickLogs.Select(Clone).ToList(),
            Events = Events.Select(Clone).ToList(),
            BattleRecords = BattleRecords.Select(Clone).ToList(),
            ChronicleEntries = ChronicleEntries.Select(Clone).ToList()
        };

    public void ReplaceWith(GameState other)
    {
        Players = other.Players;
        Cycles = other.Cycles;
        Empires = other.Empires;
        EmpireResources = other.EmpireResources;
        EmpirePriorities = other.EmpirePriorities;
        EmpireMetrics = other.EmpireMetrics;
        CycleRankings = other.CycleRankings;
        CycleMajorEvents = other.CycleMajorEvents;
        SystemHistoricalSignals = other.SystemHistoricalSignals;
        Admirals = other.Admirals;
        AdmiralBattleHistories = other.AdmiralBattleHistories;
        Systems = other.Systems;
        SystemLinks = other.SystemLinks;
        Fleets = other.Fleets;
        FleetOrders = other.FleetOrders;
        ShipConstructions = other.ShipConstructions;
        TickLogs = other.TickLogs;
        Events = other.Events;
        BattleRecords = other.BattleRecords;
        ChronicleEntries = other.ChronicleEntries;
    }

    private static Player Clone(Player item) => new()
    {
        PlayerId = item.PlayerId,
        Username = item.Username,
        Email = item.Email,
        PasswordHash = item.PasswordHash,
        Role = item.Role,
        CreatedAt = item.CreatedAt,
        LastLoginAt = item.LastLoginAt,
        Status = item.Status
    };

    private static Cycle Clone(Cycle item) => new()
    {
        CycleId = item.CycleId,
        Name = item.Name,
        StartAt = item.StartAt,
        EndAt = item.EndAt,
        TickLengthMinutes = item.TickLengthMinutes,
        CurrentTickNumber = item.CurrentTickNumber,
        Status = item.Status,
        CreatedAt = item.CreatedAt
    };

    private static Empire Clone(Empire item) => new()
    {
        EmpireId = item.EmpireId,
        CycleId = item.CycleId,
        PlayerId = item.PlayerId,
        EmpireName = item.EmpireName,
        HomeSystemId = item.HomeSystemId,
        CreatedAt = item.CreatedAt,
        Status = item.Status
    };

    private static EmpireResource Clone(EmpireResource item) => new()
    {
        EmpireResourceId = item.EmpireResourceId,
        EmpireId = item.EmpireId,
        Industry = item.Industry,
        Research = item.Research,
        Population = item.Population,
        LastGeneratedIndustry = item.LastGeneratedIndustry,
        LastGeneratedResearch = item.LastGeneratedResearch,
        LastGeneratedPopulation = item.LastGeneratedPopulation,
        LastSpentIndustry = item.LastSpentIndustry,
        LastSpentResearch = item.LastSpentResearch,
        LastSpentPopulation = item.LastSpentPopulation,
        UpdatedAt = item.UpdatedAt
    };

    private static EmpirePriority Clone(EmpirePriority item) => new()
    {
        EmpirePriorityId = item.EmpirePriorityId,
        EmpireId = item.EmpireId,
        IndustryWeight = item.IndustryWeight,
        ResearchWeight = item.ResearchWeight,
        MilitaryWeight = item.MilitaryWeight,
        ExpansionWeight = item.ExpansionWeight,
        UpdatedAt = item.UpdatedAt
    };

    private static EmpireMetric Clone(EmpireMetric item) => new()
    {
        EmpireMetricId = item.EmpireMetricId,
        CycleId = item.CycleId,
        EmpireId = item.EmpireId,
        TickNumber = item.TickNumber,
        Rank = item.Rank,
        IsWinner = item.IsWinner,
        MapControlPercent = item.MapControlPercent,
        TotalEffectivePresence = item.TotalEffectivePresence,
        ActiveShipCount = item.ActiveShipCount,
        CreatedAt = item.CreatedAt
    };

    private static CycleRanking Clone(CycleRanking item) => new()
    {
        CycleRankingId = item.CycleRankingId,
        CycleId = item.CycleId,
        EmpireId = item.EmpireId,
        Rank = item.Rank,
        IsWinner = item.IsWinner,
        MapControlPercent = item.MapControlPercent,
        TotalEffectivePresence = item.TotalEffectivePresence,
        ActiveShipCount = item.ActiveShipCount,
        CutoffTickNumber = item.CutoffTickNumber,
        CutoffAt = item.CutoffAt
    };

    private static CycleMajorEvent Clone(CycleMajorEvent item) => new()
    {
        CycleMajorEventId = item.CycleMajorEventId,
        CycleId = item.CycleId,
        SourceBattleId = item.SourceBattleId,
        SystemId = item.SystemId,
        EventType = item.EventType,
        TickNumber = item.TickNumber,
        SelectionRank = item.SelectionRank,
        ImportanceScore = item.ImportanceScore,
        TotalLosses = item.TotalLosses,
        Summary = item.Summary,
        FactJson = item.FactJson,
        CreatedAt = item.CreatedAt
    };

    private static SystemHistoricalSignal Clone(SystemHistoricalSignal item) => new()
    {
        SystemHistoricalSignalId = item.SystemHistoricalSignalId,
        CycleId = item.CycleId,
        SystemId = item.SystemId,
        SignalType = item.SignalType,
        SourceBattleId = item.SourceBattleId,
        BattleCount = item.BattleCount,
        TotalLosses = item.TotalLosses,
        LargestBattleLosses = item.LargestBattleLosses,
        HostedCycleLargestBattle = item.HostedCycleLargestBattle,
        HistoricalSignificanceIncrease = item.HistoricalSignificanceIncrease,
        HistoricalSignificanceAfter = item.HistoricalSignificanceAfter,
        Summary = item.Summary,
        FactJson = item.FactJson,
        CreatedAt = item.CreatedAt
    };

    private static Admiral Clone(Admiral item) => new()
    {
        AdmiralId = item.AdmiralId,
        CycleId = item.CycleId,
        EmpireId = item.EmpireId,
        AdmiralName = item.AdmiralName,
        ReputationScore = item.ReputationScore,
        Status = item.Status,
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt
    };

    private static AdmiralBattleHistory Clone(AdmiralBattleHistory item) => new()
    {
        AdmiralBattleHistoryId = item.AdmiralBattleHistoryId,
        CycleId = item.CycleId,
        AdmiralId = item.AdmiralId,
        BattleId = item.BattleId,
        SystemId = item.SystemId,
        FleetId = item.FleetId,
        Role = item.Role,
        Outcome = item.Outcome,
        ShipsCommandedBefore = item.ShipsCommandedBefore,
        ShipsLost = item.ShipsLost,
        ReputationChange = item.ReputationChange,
        ReputationScoreAfter = item.ReputationScoreAfter,
        AdmiralStatusAfter = item.AdmiralStatusAfter,
        IsFamousSystemAssociation = item.IsFamousSystemAssociation,
        CreatedAt = item.CreatedAt
    };

    private static GalaxySystem Clone(GalaxySystem item) => new()
    {
        SystemId = item.SystemId,
        CycleId = item.CycleId,
        SystemName = item.SystemName,
        X = item.X,
        Y = item.Y,
        IndustryOutput = item.IndustryOutput,
        ResearchOutput = item.ResearchOutput,
        PopulationOutput = item.PopulationOutput,
        StrategicValue = item.StrategicValue,
        HistoricalSignificance = item.HistoricalSignificance,
        CreatedAt = item.CreatedAt
    };

    private static SystemLink Clone(SystemLink item) => new()
    {
        SystemLinkId = item.SystemLinkId,
        CycleId = item.CycleId,
        SystemAId = item.SystemAId,
        SystemBId = item.SystemBId,
        Distance = item.Distance,
        TravelTicks = item.TravelTicks
    };

    private static Fleet Clone(Fleet item) => new()
    {
        FleetId = item.FleetId,
        CycleId = item.CycleId,
        EmpireId = item.EmpireId,
        AdmiralId = item.AdmiralId,
        FleetName = item.FleetName,
        CurrentSystemId = item.CurrentSystemId,
        DestinationSystemId = item.DestinationSystemId,
        ArrivalTickNumber = item.ArrivalTickNumber,
        ShipCount = item.ShipCount,
        Status = item.Status,
        CreatedAt = item.CreatedAt
    };

    private static FleetOrder Clone(FleetOrder item) => new()
    {
        FleetOrderId = item.FleetOrderId,
        CycleId = item.CycleId,
        FleetId = item.FleetId,
        OrderType = item.OrderType,
        TargetSystemId = item.TargetSystemId,
        TargetEmpireId = item.TargetEmpireId,
        SubmitTick = item.SubmitTick,
        ExecuteAfterTick = item.ExecuteAfterTick,
        ProcessedTick = item.ProcessedTick,
        Status = item.Status,
        RejectionReason = item.RejectionReason,
        CreatedAt = item.CreatedAt
    };

    private static ShipConstruction Clone(ShipConstruction item) => new()
    {
        ShipConstructionId = item.ShipConstructionId,
        CycleId = item.CycleId,
        EmpireId = item.EmpireId,
        ShipCount = item.ShipCount,
        IndustrySpent = item.IndustrySpent,
        StartedTick = item.StartedTick,
        CompleteAfterTick = item.CompleteAfterTick,
        CompletedTick = item.CompletedTick,
        Status = item.Status,
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt
    };

    private static TickLog Clone(TickLog item) => new()
    {
        TickLogId = item.TickLogId,
        CycleId = item.CycleId,
        TickNumber = item.TickNumber,
        StartedAt = item.StartedAt,
        CompletedAt = item.CompletedAt,
        Status = item.Status,
        DiagnosticLog = item.DiagnosticLog
    };

    private static EventRecord Clone(EventRecord item) => new()
    {
        EventId = item.EventId,
        CycleId = item.CycleId,
        TickNumber = item.TickNumber,
        EventType = item.EventType,
        SystemId = item.SystemId,
        EmpireId = item.EmpireId,
        Severity = item.Severity,
        FactJson = item.FactJson,
        DisplayText = item.DisplayText,
        CreatedAt = item.CreatedAt
    };

    private static BattleRecord Clone(BattleRecord item) => new()
    {
        BattleId = item.BattleId,
        CycleId = item.CycleId,
        TickNumber = item.TickNumber,
        SystemId = item.SystemId,
        AttackerEmpireId = item.AttackerEmpireId,
        DefenderEmpireId = item.DefenderEmpireId,
        AttackerFleetIds = item.AttackerFleetIds,
        DefenderFleetIds = item.DefenderFleetIds,
        AttackerShipsBefore = item.AttackerShipsBefore,
        DefenderShipsBefore = item.DefenderShipsBefore,
        AttackerLosses = item.AttackerLosses,
        DefenderLosses = item.DefenderLosses,
        Outcome = item.Outcome,
        FactJson = item.FactJson,
        CreatedAt = item.CreatedAt
    };

    private static ChronicleEntry Clone(ChronicleEntry item) => new()
    {
        ChronicleEntryId = item.ChronicleEntryId,
        SourceEventId = item.SourceEventId,
        SourceBattleId = item.SourceBattleId,
        CycleId = item.CycleId,
        SystemId = item.SystemId,
        Title = item.Title,
        EntryType = item.EntryType,
        ImportanceScore = item.ImportanceScore,
        FactualSummary = item.FactualSummary,
        NarrativeText = item.NarrativeText,
        NarrativeStatus = item.NarrativeStatus,
        NarrativeContextJson = item.NarrativeContextJson,
        NarrativeGeneratedAt = item.NarrativeGeneratedAt,
        NarrativeFailureReason = item.NarrativeFailureReason,
        CreatedAt = item.CreatedAt
    };
}

public sealed class Player
{
    public Guid PlayerId { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public PlayerRole Role { get; set; } = PlayerRole.Player;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public PlayerStatus Status { get; set; } = PlayerStatus.Active;
}

public sealed class Cycle
{
    public Guid CycleId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public int TickLengthMinutes { get; set; } = 60;
    public int CurrentTickNumber { get; set; }
    public CycleStatus Status { get; set; } = CycleStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Empire
{
    public Guid EmpireId { get; set; } = Guid.NewGuid();
    public Guid CycleId { get; set; }
    public Guid PlayerId { get; set; }
    public string EmpireName { get; set; } = "";
    public Guid HomeSystemId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public EmpireStatus Status { get; set; } = EmpireStatus.Active;
}

public sealed class EmpireResource
{
    public Guid EmpireResourceId { get; set; } = Guid.NewGuid();
    public Guid EmpireId { get; set; }
    public decimal Industry { get; set; }
    public decimal Research { get; set; }
    public decimal Population { get; set; }
    public decimal LastGeneratedIndustry { get; set; }
    public decimal LastGeneratedResearch { get; set; }
    public decimal LastGeneratedPopulation { get; set; }
    public decimal LastSpentIndustry { get; set; }
    public decimal LastSpentResearch { get; set; }
    public decimal LastSpentPopulation { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class EmpirePriority
{
    public Guid EmpirePriorityId { get; set; } = Guid.NewGuid();
    public Guid EmpireId { get; set; }
    public int IndustryWeight { get; set; } = 25;
    public int ResearchWeight { get; set; } = 25;
    public int MilitaryWeight { get; set; } = 25;
    public int ExpansionWeight { get; set; } = 25;
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class EmpireMetric
{
    public Guid EmpireMetricId { get; set; } = Guid.NewGuid();
    public Guid CycleId { get; set; }
    public Guid EmpireId { get; set; }
    public int TickNumber { get; set; }
    public int Rank { get; set; }
    public bool IsWinner { get; set; }
    public decimal MapControlPercent { get; set; }
    public decimal TotalEffectivePresence { get; set; }
    public int ActiveShipCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class CycleRanking
{
    public Guid CycleRankingId { get; set; } = Guid.NewGuid();
    public Guid CycleId { get; set; }
    public Guid EmpireId { get; set; }
    public int Rank { get; set; }
    public bool IsWinner { get; set; }
    public decimal MapControlPercent { get; set; }
    public decimal TotalEffectivePresence { get; set; }
    public int ActiveShipCount { get; set; }
    public int CutoffTickNumber { get; set; }
    public DateTimeOffset CutoffAt { get; set; }
}

public sealed class CycleMajorEvent
{
    public Guid CycleMajorEventId { get; set; } = Guid.NewGuid();
    public Guid CycleId { get; set; }
    public Guid? SourceBattleId { get; set; }
    public Guid? SystemId { get; set; }
    public CycleMajorEventType EventType { get; set; }
    public int TickNumber { get; set; }
    public int SelectionRank { get; set; }
    public int ImportanceScore { get; set; }
    public int TotalLosses { get; set; }
    public string Summary { get; set; } = "";
    public string FactJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class SystemHistoricalSignal
{
    public Guid SystemHistoricalSignalId { get; set; } = Guid.NewGuid();
    public Guid CycleId { get; set; }
    public Guid SystemId { get; set; }
    public SystemHistoricalSignalType SignalType { get; set; }
    public Guid? SourceBattleId { get; set; }
    public int BattleCount { get; set; }
    public int TotalLosses { get; set; }
    public int LargestBattleLosses { get; set; }
    public bool HostedCycleLargestBattle { get; set; }
    public int HistoricalSignificanceIncrease { get; set; }
    public int HistoricalSignificanceAfter { get; set; }
    public string Summary { get; set; } = "";
    public string FactJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class GalaxySystem
{
    public Guid SystemId { get; set; } = Guid.NewGuid();
    public Guid CycleId { get; set; }
    public string SystemName { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public decimal IndustryOutput { get; set; }
    public decimal ResearchOutput { get; set; }
    public decimal PopulationOutput { get; set; }
    public int StrategicValue { get; set; }
    public int HistoricalSignificance { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class SystemLink
{
    public Guid SystemLinkId { get; set; } = Guid.NewGuid();
    public Guid CycleId { get; set; }
    public Guid SystemAId { get; set; }
    public Guid SystemBId { get; set; }
    public decimal Distance { get; set; }
    public int TravelTicks { get; set; } = 1;

    public bool Connects(Guid firstSystemId, Guid secondSystemId) =>
        (SystemAId == firstSystemId && SystemBId == secondSystemId)
        || (SystemAId == secondSystemId && SystemBId == firstSystemId);
}

public sealed class Fleet
{
    public Guid FleetId { get; set; } = Guid.NewGuid();
    public Guid CycleId { get; set; }
    public Guid EmpireId { get; set; }
    public Guid? AdmiralId { get; set; }
    public string FleetName { get; set; } = "";
    public Guid CurrentSystemId { get; set; }
    public Guid? DestinationSystemId { get; set; }
    public int? ArrivalTickNumber { get; set; }
    public int ShipCount { get; set; }
    public FleetStatus Status { get; set; } = FleetStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Admiral
{
    public Guid AdmiralId { get; set; } = Guid.NewGuid();
    public Guid CycleId { get; set; }
    public Guid EmpireId { get; set; }
    public string AdmiralName { get; set; } = "";
    public int ReputationScore { get; set; }
    public AdmiralStatus Status { get; set; } = AdmiralStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class AdmiralBattleHistory
{
    public Guid AdmiralBattleHistoryId { get; set; } = Guid.NewGuid();
    public Guid CycleId { get; set; }
    public Guid AdmiralId { get; set; }
    public Guid BattleId { get; set; }
    public Guid SystemId { get; set; }
    public Guid FleetId { get; set; }
    public AdmiralBattleRole Role { get; set; }
    public AdmiralBattleOutcome Outcome { get; set; }
    public int ShipsCommandedBefore { get; set; }
    public int ShipsLost { get; set; }
    public int ReputationChange { get; set; }
    public int ReputationScoreAfter { get; set; }
    public AdmiralStatus AdmiralStatusAfter { get; set; }
    public bool IsFamousSystemAssociation { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class FleetOrder
{
    public Guid FleetOrderId { get; set; } = Guid.NewGuid();
    public Guid CycleId { get; set; }
    public Guid FleetId { get; set; }
    public FleetOrderType OrderType { get; set; }
    public Guid? TargetSystemId { get; set; }
    public Guid? TargetEmpireId { get; set; }
    public int SubmitTick { get; set; }
    public int ExecuteAfterTick { get; set; }
    public int? ProcessedTick { get; set; }
    public FleetOrderStatus Status { get; set; } = FleetOrderStatus.Pending;
    public string? RejectionReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ShipConstruction
{
    public Guid ShipConstructionId { get; set; } = Guid.NewGuid();
    public Guid CycleId { get; set; }
    public Guid EmpireId { get; set; }
    public int ShipCount { get; set; }
    public decimal IndustrySpent { get; set; }
    public int StartedTick { get; set; }
    public int CompleteAfterTick { get; set; }
    public int? CompletedTick { get; set; }
    public ShipConstructionStatus Status { get; set; } = ShipConstructionStatus.Queued;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class TickLog
{
    public Guid TickLogId { get; set; } = Guid.NewGuid();
    public Guid CycleId { get; set; }
    public int TickNumber { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public TickLogStatus Status { get; set; }
    public string DiagnosticLog { get; set; } = "";
}

public sealed class EventRecord
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid CycleId { get; set; }
    public int TickNumber { get; set; }
    public EventType EventType { get; set; }
    public Guid? SystemId { get; set; }
    public Guid? EmpireId { get; set; }
    public EventSeverity Severity { get; set; } = EventSeverity.Normal;
    public string FactJson { get; set; } = "{}";
    public string DisplayText { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class BattleRecord
{
    public Guid BattleId { get; set; } = Guid.NewGuid();
    public Guid CycleId { get; set; }
    public int TickNumber { get; set; }
    public Guid SystemId { get; set; }
    public Guid AttackerEmpireId { get; set; }
    public Guid DefenderEmpireId { get; set; }
    public string AttackerFleetIds { get; set; } = "";
    public string DefenderFleetIds { get; set; } = "";
    public int AttackerShipsBefore { get; set; }
    public int DefenderShipsBefore { get; set; }
    public int AttackerLosses { get; set; }
    public int DefenderLosses { get; set; }
    public BattleOutcome Outcome { get; set; }
    public string FactJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ChronicleEntry
{
    public Guid ChronicleEntryId { get; set; } = Guid.NewGuid();
    public Guid? SourceEventId { get; set; }
    public Guid? SourceBattleId { get; set; }
    public Guid CycleId { get; set; }
    public Guid? SystemId { get; set; }
    public string Title { get; set; } = "";
    public ChronicleEntryType EntryType { get; set; }
    public int ImportanceScore { get; set; }
    public string FactualSummary { get; set; } = "";
    public string NarrativeText { get; set; } = "";
    public NarrativeGenerationStatus NarrativeStatus { get; set; } = NarrativeGenerationStatus.Generated;
    public string NarrativeContextJson { get; set; } = "{}";
    public DateTimeOffset? NarrativeGeneratedAt { get; set; }
    public string? NarrativeFailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public enum PlayerStatus
{
    Active,
    Suspended
}

public enum PlayerRole
{
    Player,
    Admin
}

public enum CycleStatus
{
    Active,
    Completed,
    RecoveryRequired
}

public enum EmpireStatus
{
    Active,
    Defeated
}

public enum FleetStatus
{
    Active,
    InTransit,
    Destroyed
}

public enum AdmiralStatus
{
    Active,
    Retired,
    Killed,
    Missing,
    Legendary
}

public enum AdmiralBattleRole
{
    Attacker,
    Defender
}

public enum AdmiralBattleOutcome
{
    Victory,
    Defeat,
    MutualDestruction
}

public enum FleetOrderType
{
    MoveFleet,
    Hold,
    Attack
}

public enum FleetOrderStatus
{
    Pending,
    Processed,
    Rejected,
    Cancelled
}

public enum ShipConstructionStatus
{
    Queued,
    Completed
}

public enum TickLogStatus
{
    Running,
    Completed,
    Failed
}

public enum EventType
{
    CycleSeeded,
    ResourcesGenerated,
    ShipConstructionQueued,
    ShipConstructionCompleted,
    FleetMoved,
    FleetArrived,
    FleetHeld,
    OrderRejected,
    OrderCancelled,
    CombatResolved,
    PrioritiesChanged,
    ChronicleCreated,
    RecoveryCleared,
    CycleCompleted,
    DoctrineUnlocked,
    AdmiralBattleReported
}

public enum EventSeverity
{
    Low,
    Normal,
    High,
    Historic
}

public enum BattleOutcome
{
    AttackerVictory,
    DefenderVictory,
    MutualDestruction
}

public enum ChronicleEntryType
{
    Battle,
    Cycle,
    System,
    Diplomacy,
    Discovery
}

public enum NarrativeGenerationStatus
{
    NotQueued,
    Pending,
    Generated,
    Failed
}

public enum CycleMajorEventType
{
    Battle
}

public enum SystemHistoricalSignalType
{
    BattleActivity
}
