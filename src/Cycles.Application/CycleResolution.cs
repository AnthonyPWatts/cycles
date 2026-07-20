using Cycles.Core;

namespace Cycles.Application;

public sealed record DueCycleWorkItem
{
    public DueCycleWorkItem(GameCycleScope scope, DateTimeOffset nextTickAt)
    {
        ArgumentNullException.ThrowIfNull(scope);
        Scope = scope;
        NextTickAt = nextTickAt;
    }

    public GameCycleScope Scope { get; }

    public DateTimeOffset NextTickAt { get; }
}

public interface IDueCycleQuery
{
    DueCycleWorkItem? GetNextDue(DateTimeOffset now);
}

public interface ICycleResolutionStore
{
    CycleResolutionResult ResolveIfDue(
        DueCycleWorkItem workItem,
        DateTimeOffset now);

    CycleResolutionResult ResolveExplicit(
        ExplicitCycleResolutionRequest request,
        DateTimeOffset now);
}

public sealed record ExplicitCycleResolutionRequest
{
    public ExplicitCycleResolutionRequest(
        GameCommandContext context,
        bool requireAdminister,
        bool requireActiveTutorialRun = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
        RequireAdminister = requireAdminister;
        RequireActiveTutorialRun = requireActiveTutorialRun;
    }

    public GameCommandContext Context { get; }

    public bool RequireAdminister { get; }

    public bool RequireActiveTutorialRun { get; }

    public GameCycleScope Scope => Context.Scope;
}

public abstract record CycleResolutionResult
{
    private CycleResolutionResult()
    {
    }

    public sealed record Completed(TickResult Value) : CycleResolutionResult;

    public sealed record RecoveryRequired(TickResult Value) : CycleResolutionResult;

    public sealed record NotDue() : CycleResolutionResult;

    public sealed record Stale() : CycleResolutionResult;

    public sealed record Unavailable() : CycleResolutionResult;

    public sealed record Forbidden() : CycleResolutionResult;

    public sealed record Busy() : CycleResolutionResult;
}
