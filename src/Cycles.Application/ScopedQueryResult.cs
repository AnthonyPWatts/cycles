namespace Cycles.Application;

public abstract record ScopedQueryResult<T>
{
    private ScopedQueryResult()
    {
    }

    public sealed record Success(T Value) : ScopedQueryResult<T>;

    public sealed record Unavailable() : ScopedQueryResult<T>;
}
