using Cycles.Core;

namespace Cycles.Application;

public interface IPlayerAccountQuery
{
    PlayerAccountSnapshot? Get(Guid playerId);
}

public interface IGameCatalogueQuery
{
    GameCataloguePage ListForPlayer(
        Guid playerId,
        GameCatalogueCursor? cursor,
        int pageSize);
}

public interface IGameAccessQuery
{
    GameAccessSnapshot? Get(Guid playerId, Guid gameId);
}

public interface ICycleCommandStore
{
    ScopedCommandResult<T> Execute<T>(
        GameCycleScope scope,
        Func<GameState, T> command);
}
