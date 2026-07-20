using Cycles.Application;
using Cycles.Core;

public sealed class SelectedGameRequestService(
    IPlayerAccountQuery accounts,
    IGameAccessQuery gameAccess,
    IGameCommandAccessQuery commandAccess,
    ICycleViewQuery cycleViews,
    ICycleCommandStore cycleCommands)
{
    public PlayerAccountSnapshot RequireAccount(HttpContext httpContext) =>
        DevelopmentAuth.RequireAccount(httpContext, accounts);

    public GameCommandContext RequireContext(HttpContext httpContext, Guid gameId)
    {
        var account = RequireAccount(httpContext);
        var access = gameAccess.Get(account.PlayerId, gameId)
            ?? throw new ApiNotFoundException("Game is unavailable.");
        if (access.EnrolmentStatus == GameEnrolmentStatus.Withdrawn)
        {
            throw new ApiNotFoundException("Game is unavailable.");
        }

        var cycleId = access.OperationalCycleId
            ?? throw new ApiStateConflictException("The selected Game has no current Cycle.");
        var context = commandAccess.Get(account.PlayerId, new GameCycleScope(gameId, cycleId))
            ?? throw new ApiStateConflictException("The selected Game is not currently playable by this player.");

        return context;
    }

    public T Query<T>(
        HttpContext httpContext,
        Guid gameId,
        Func<GameState, GameCommandContext, T> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var context = RequireContext(httpContext, gameId);
        return cycleViews.Query(context, state => projection(state, context)) switch
        {
            ScopedQueryResult<T>.Success success => success.Value,
            ScopedQueryResult<T>.Unavailable =>
                throw new ApiNotFoundException("Game is unavailable."),
            _ => throw new InvalidOperationException("The Cycle view returned an unsupported query result.")
        };
    }

    public T Command<T>(
        HttpContext httpContext,
        Guid gameId,
        Func<GameState, GameCommandContext, T> command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var context = RequireContext(httpContext, gameId);
        return cycleCommands.Execute(context, state => command(state, context)) switch
        {
            ScopedCommandResult<T>.Success success => success.Value,
            ScopedCommandResult<T>.Unavailable =>
                throw new ApiForbiddenException("The selected Game's current Cycle no longer accepts commands from this player."),
            ScopedCommandResult<T>.Busy =>
                throw new ApiStateConflictException("The selected Game is temporarily busy. Try again."),
            _ => throw new InvalidOperationException("The Cycle command store returned an unsupported command result.")
        };
    }
}
