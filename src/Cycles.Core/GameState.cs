using System.Text.Json;

namespace Cycles.Core;

public sealed class GameState
{
    public List<Player> Players { get; set; } = [];
    public List<Cycle> Cycles { get; set; } = [];
    public List<Empire> Empires { get; set; } = [];
    public List<EmpireResource> EmpireResources { get; set; } = [];
    public List<EmpirePriority> EmpirePriorities { get; set; } = [];
    public List<GalaxySystem> Systems { get; set; } = [];
    public List<SystemLink> SystemLinks { get; set; } = [];
    public List<Fleet> Fleets { get; set; } = [];
    public List<FleetOrder> FleetOrders { get; set; } = [];
    public List<TickLog> TickLogs { get; set; } = [];
    public List<EventRecord> Events { get; set; } = [];
    public List<BattleRecord> BattleRecords { get; set; } = [];
    public List<ChronicleEntry> ChronicleEntries { get; set; } = [];

    public Cycle? GetActiveCycle() =>
        Cycles
            .Where(cycle => cycle.Status == CycleStatus.Active)
            .OrderByDescending(cycle => cycle.StartAt)
            .FirstOrDefault();

    public GameState DeepClone()
    {
        var json = JsonSerializer.Serialize(this, GameStateJson.Options);
        return JsonSerializer.Deserialize<GameState>(json, GameStateJson.Options)
            ?? throw new InvalidOperationException("Could not clone game state.");
    }

    public void ReplaceWith(GameState other)
    {
        Players = other.Players;
        Cycles = other.Cycles;
        Empires = other.Empires;
        EmpireResources = other.EmpireResources;
        EmpirePriorities = other.EmpirePriorities;
        Systems = other.Systems;
        SystemLinks = other.SystemLinks;
        Fleets = other.Fleets;
        FleetOrders = other.FleetOrders;
        TickLogs = other.TickLogs;
        Events = other.Events;
        BattleRecords = other.BattleRecords;
        ChronicleEntries = other.ChronicleEntries;
    }
}

public sealed class Player
{
    public Guid PlayerId { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
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
    public string FleetName { get; set; } = "";
    public Guid CurrentSystemId { get; set; }
    public Guid? DestinationSystemId { get; set; }
    public int? ArrivalTickNumber { get; set; }
    public int ShipCount { get; set; }
    public FleetStatus Status { get; set; } = FleetStatus.Active;
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
    public DateTimeOffset CreatedAt { get; set; }
}

public enum PlayerStatus
{
    Active,
    Suspended
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
    Rejected
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
    FleetMoved,
    FleetArrived,
    FleetHeld,
    OrderRejected,
    CombatResolved,
    PrioritiesChanged,
    ChronicleCreated
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
