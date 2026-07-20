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

public interface IGameCommandAccessQuery
{
    GameCommandContext? Get(Guid playerId, GameCycleScope scope);
}

public interface ICycleViewQuery
{
    ScopedQueryResult<T> Query<T>(
        GameCommandContext context,
        Func<GameState, T> projection);
}

public interface ICycleCommandStore
{
    ScopedCommandResult<T> Execute<T>(
        GameCommandContext context,
        Func<GameState, T> command);
}

public interface ILegacyRuntimeScopeQuery
{
    GameCycleScope GetRequired();
}
