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

public enum ExplicitCycleResolutionPolicy
{
    Administrator,
    DevelopmentStandard,
    SelfPacedParticipant,
    TutorialJourney
}

public sealed record ExplicitCycleResolutionRequest
{
    public ExplicitCycleResolutionRequest(
        GameCommandContext context,
        bool requireAdminister,
        bool requireActiveTutorialRun = false)
        : this(
            context,
            requireActiveTutorialRun
                ? ExplicitCycleResolutionPolicy.TutorialJourney
                : requireAdminister
                    ? ExplicitCycleResolutionPolicy.Administrator
                    : ExplicitCycleResolutionPolicy.DevelopmentStandard)
    {
    }

    public ExplicitCycleResolutionRequest(
        GameCommandContext context,
        ExplicitCycleResolutionPolicy policy,
        int? expectedCurrentTickNumber = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Enum.IsDefined(policy))
        {
            throw new ArgumentOutOfRangeException(nameof(policy), policy, "The explicit resolution policy is invalid.");
        }
        if (expectedCurrentTickNumber < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedCurrentTickNumber),
                expectedCurrentTickNumber,
                "The expected current tick number cannot be negative.");
        }

        Context = context;
        Policy = policy;
        ExpectedCurrentTickNumber = expectedCurrentTickNumber;
    }

    public GameCommandContext Context { get; }

    public ExplicitCycleResolutionPolicy Policy { get; }

    public int? ExpectedCurrentTickNumber { get; }

    public bool RequireAdminister => Policy == ExplicitCycleResolutionPolicy.Administrator;

    public bool RequireActiveTutorialRun => Policy == ExplicitCycleResolutionPolicy.TutorialJourney;

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
