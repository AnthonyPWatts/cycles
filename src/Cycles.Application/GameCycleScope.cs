namespace Cycles.Application;

public sealed record GameCycleScope
{
    public GameCycleScope(Guid gameId, Guid cycleId)
    {
        if (gameId == Guid.Empty)
        {
            throw new ArgumentException("Game identifier cannot be empty.", nameof(gameId));
        }

        if (cycleId == Guid.Empty)
        {
            throw new ArgumentException("Cycle identifier cannot be empty.", nameof(cycleId));
        }

        GameId = gameId;
        CycleId = cycleId;
    }

    public Guid GameId { get; }

    public Guid CycleId { get; }
}
