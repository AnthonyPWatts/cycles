namespace Cycles.Application;

public abstract record ScopedCommandResult<T>
{
    private ScopedCommandResult()
    {
    }

    public sealed record Success(T Value) : ScopedCommandResult<T>;

    public sealed record Unavailable() : ScopedCommandResult<T>;

    public sealed record Busy() : ScopedCommandResult<T>;
}
