namespace Cycles.Core;

public interface IGameStateStore
{
    string Description { get; }

    GameState LoadOrCreate();

    T Update<T>(Func<GameState, T> update);

    void Replace(GameState state);
}
