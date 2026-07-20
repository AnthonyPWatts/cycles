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
    DateTimeOffset CreatedAt);

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
