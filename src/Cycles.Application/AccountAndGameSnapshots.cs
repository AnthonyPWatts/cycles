using Cycles.Core;

namespace Cycles.Application;

/// <summary>
/// A player projection that deliberately excludes credentials, email and external identity claims.
/// </summary>
public sealed record PlayerAccountSnapshot(
    Guid PlayerId,
    string Username,
    PlayerKind Kind,
    PlayerRole Role,
    PlayerStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

public sealed record GameCatalogueItem(
    Guid GameId,
    string GameName,
    GamePurpose Purpose,
    GameLifecycleStatus GameStatus,
    GameVisibility Visibility,
    Guid GameEnrolmentId,
    GameEnrolmentStatus EnrolmentStatus,
    DateTimeOffset EnrolmentStatusChangedAt,
    Guid? OperationalCycleId,
    CycleStatus? OperationalCycleStatus,
    int? CurrentTickNumber,
    TurnResolutionStage? TurnStage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FirstStartedAt = null,
    int? TickLengthMinutes = null,
    DateTimeOffset? NextTickAt = null);

public enum GamesHomeAction
{
    Continue,
    EnterLobby,
    Observe,
    Review
}

public enum GamesHomeAttentionReason
{
    RecoveryRequired,
    CommandsCloseSoon,
    GameStarted,
    TrainingInProgress
}

public sealed record GamesHomeItem(
    GameCatalogueItem Game,
    GamesHomeAction Action,
    int? AttentionRank,
    GamesHomeAttentionReason? AttentionReason);

public sealed record GamesHomeSnapshot(
    IReadOnlyList<GamesHomeItem> NeedsAttention,
    int TotalAttentionCount,
    IReadOnlyList<GamesHomeItem> ActiveGames,
    IReadOnlyList<GamesHomeItem> WaitingGames,
    IReadOnlyList<GamesHomeItem> CompletedGames,
    bool HasMore,
    TrainingGameOffer? Training = null);

public sealed record TrainingGameOffer(
    string TutorialKey,
    string DisplayName,
    int EstimatedMinutes);

public static class GamesHomeProjection
{
    private const int AttentionLimit = 3;

    public static GamesHomeSnapshot Create(GameCataloguePage page, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(page);

        var projected = page.Items
            .Select(game => new GamesHomeItem(
                game,
                ActionFor(game),
                AttentionRank: null,
                AttentionReason: AttentionReasonFor(game, now)))
            .ToArray();
        var rankedAttention = projected
            .Where(item => item.AttentionReason is not null)
            .OrderBy(item => AttentionPriority(item.AttentionReason!.Value))
            .ThenBy(item => item.Game.NextTickAt ?? item.Game.FirstStartedAt ?? item.Game.CreatedAt)
            .ThenBy(item => item.Game.GameId)
            .Select((item, index) => item with { AttentionRank = index + 1 })
            .ToArray();
        var ranks = rankedAttention.ToDictionary(item => item.Game.GameId);
        projected = projected
            .Select(item => ranks.GetValueOrDefault(item.Game.GameId, item))
            .ToArray();

        return new GamesHomeSnapshot(
            Array.AsReadOnly(rankedAttention.Take(AttentionLimit).ToArray()),
            rankedAttention.Length,
            Array.AsReadOnly(projected
                .Where(item => item.Game.GameStatus == GameLifecycleStatus.Active)
                .OrderBy(item => item.AttentionRank ?? int.MaxValue)
                .ThenBy(item => item.Game.GameName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Game.GameId)
                .ToArray()),
            Array.AsReadOnly(projected
                .Where(item => item.Game.GameStatus is GameLifecycleStatus.Forming
                    or GameLifecycleStatus.Starting
                    or GameLifecycleStatus.Intermission)
                .OrderBy(item => item.Game.GameName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Game.GameId)
                .ToArray()),
            Array.AsReadOnly(projected
                .Where(item => item.Game.GameStatus is GameLifecycleStatus.Completed
                    or GameLifecycleStatus.Cancelled
                    or GameLifecycleStatus.Terminated)
                .OrderByDescending(item => item.Game.EnrolmentStatusChangedAt)
                .ThenBy(item => item.Game.GameId)
                .ToArray()),
            page.HasMore);
    }

    private static GamesHomeAction ActionFor(GameCatalogueItem game) => game switch
    {
        { EnrolmentStatus: GameEnrolmentStatus.Withdrawn } => GamesHomeAction.Review,
        { GameStatus: GameLifecycleStatus.Active, OperationalCycleStatus: CycleStatus.Active } =>
            GamesHomeAction.Continue,
        { GameStatus: GameLifecycleStatus.Active } => GamesHomeAction.Observe,
        { GameStatus: GameLifecycleStatus.Forming or GameLifecycleStatus.Starting or GameLifecycleStatus.Intermission } =>
            GamesHomeAction.EnterLobby,
        _ => GamesHomeAction.Review
    };

    private static GamesHomeAttentionReason? AttentionReasonFor(
        GameCatalogueItem game,
        DateTimeOffset now)
    {
        if (game.EnrolmentStatus != GameEnrolmentStatus.Enrolled)
        {
            return null;
        }
        if (game.OperationalCycleStatus == CycleStatus.RecoveryRequired)
        {
            return GamesHomeAttentionReason.RecoveryRequired;
        }
        if (game.GameStatus != GameLifecycleStatus.Active
            || game.OperationalCycleStatus != CycleStatus.Active)
        {
            return null;
        }
        if (game.NextTickAt is not null)
        {
            return GamesHomeAttentionReason.CommandsCloseSoon;
        }
        if (game.FirstStartedAt is not null && game.FirstStartedAt >= now.AddHours(-24))
        {
            return GamesHomeAttentionReason.GameStarted;
        }
        return game.Purpose == GamePurpose.Training
            ? GamesHomeAttentionReason.TrainingInProgress
            : null;
    }

    private static int AttentionPriority(GamesHomeAttentionReason reason) => reason switch
    {
        GamesHomeAttentionReason.RecoveryRequired => 0,
        GamesHomeAttentionReason.CommandsCloseSoon => 1,
        GamesHomeAttentionReason.GameStarted => 2,
        GamesHomeAttentionReason.TrainingInProgress => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
    };
}

public sealed record GameCatalogueCursor
{
    public GameCatalogueCursor(DateTimeOffset sortAt, Guid gameId)
    {
        if (gameId == Guid.Empty)
        {
            throw new ArgumentException("Game identifier cannot be empty.", nameof(gameId));
        }

        SortAt = sortAt;
        GameId = gameId;
    }

    public DateTimeOffset SortAt { get; }

    public Guid GameId { get; }
}

public sealed record GameCataloguePage
{
    public const int DefaultPageSize = 25;
    public const int MaximumPageSize = 100;

    public GameCataloguePage(
        IEnumerable<GameCatalogueItem> items,
        GameCatalogueCursor? nextCursor)
    {
        ArgumentNullException.ThrowIfNull(items);

        var materialisedItems = items.ToArray();
        if (materialisedItems.Length > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(items),
                materialisedItems.Length,
                $"A Game catalogue page cannot contain more than {MaximumPageSize} items.");
        }

        Items = Array.AsReadOnly(materialisedItems);
        NextCursor = nextCursor;
    }

    public IReadOnlyList<GameCatalogueItem> Items { get; }

    public GameCatalogueCursor? NextCursor { get; }

    public bool HasMore => NextCursor is not null;

    public static void ValidatePageSize(int pageSize)
    {
        if (pageSize is < 1 or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                $"Page size must be between 1 and {MaximumPageSize}.");
        }
    }
}

public sealed record GameAccessSnapshot(
    Guid PlayerId,
    Guid GameId,
    string GameName,
    GamePurpose Purpose,
    GameLifecycleStatus GameStatus,
    GameVisibility Visibility,
    Guid? CreatedByPlayerId,
    Guid? GameEnrolmentId,
    GameEnrolmentStatus? EnrolmentStatus,
    Guid? OperationalCycleId,
    CycleStatus? OperationalCycleStatus,
    int? CurrentTickNumber,
    TurnResolutionStage? TurnStage);
