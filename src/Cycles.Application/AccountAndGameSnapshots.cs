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

public sealed record GamesHomeItem(
    GameCatalogueItem Game,
    GamesHomeAction Action,
    DateTimeOffset? CommandDeadline);

public sealed record GamesHomeSnapshot(
    IReadOnlyList<GamesHomeItem> Games,
    bool HasMore,
    IReadOnlyList<TrainingGameOffer> Tutorials);

public sealed record TrainingGameOffer(
    string TutorialKey,
    string DisplayName,
    int EstimatedMinutes);

public static class GamesHomeProjection
{
    public static GamesHomeSnapshot Create(GameCataloguePage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var projected = page.Items
            .Select(game => new GamesHomeItem(
                game,
                ActionFor(game),
                CurrentPlayerDeadline(game)))
            .OrderBy(item => ListGroup(item.Game))
            .ThenBy(item => CurrentPlayerDeadline(item.Game) ?? DateTimeOffset.MaxValue)
            .ThenBy(item => UntimedPriority(item.Game))
            .ThenByDescending(item => item.Game.EnrolmentStatusChangedAt)
            .ThenBy(item => item.Game.GameName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Game.GameId)
            .ToArray();

        return new GamesHomeSnapshot(
            Array.AsReadOnly(projected),
            page.HasMore,
            Array.Empty<TrainingGameOffer>());
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

    private static int ListGroup(GameCatalogueItem game)
    {
        if (game.Purpose == GamePurpose.Training
            && game.EnrolmentStatus == GameEnrolmentStatus.Enrolled
            && game.GameStatus is GameLifecycleStatus.Active or GameLifecycleStatus.Intermission)
        {
            return 0;
        }

        return CurrentPlayerDeadline(game) is not null ? 1 : 2;
    }

    private static DateTimeOffset? CurrentPlayerDeadline(GameCatalogueItem game) => game is
    {
        EnrolmentStatus: GameEnrolmentStatus.Enrolled,
        GameStatus: GameLifecycleStatus.Active,
        OperationalCycleStatus: CycleStatus.Active,
        TurnStage: TurnResolutionStage.CommandOpen,
        NextTickAt: not null
    }
        ? game.NextTickAt
        : null;

    private static int UntimedPriority(GameCatalogueItem game) => game switch
    {
        { EnrolmentStatus: GameEnrolmentStatus.Withdrawn } => 5,
        { OperationalCycleStatus: CycleStatus.RecoveryRequired } => 0,
        {
            GameStatus: GameLifecycleStatus.Active,
            OperationalCycleStatus: CycleStatus.Active,
            TurnStage: TurnResolutionStage.CommandOpen
        } => 1,
        { GameStatus: GameLifecycleStatus.Active } => 2,
        { GameStatus: GameLifecycleStatus.Forming or GameLifecycleStatus.Starting or GameLifecycleStatus.Intermission } => 3,
        { GameStatus: GameLifecycleStatus.Completed or GameLifecycleStatus.Cancelled or GameLifecycleStatus.Terminated } => 4,
        _ => 6
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
