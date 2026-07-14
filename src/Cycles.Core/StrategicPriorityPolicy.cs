namespace Cycles.Core;

public static class StrategicPriorityPolicy
{
    public const int TotalWeight = 100;
    public const int DefaultMilitaryWeight = 67;
    public const int DefaultExpansionWeight = 33;

    public static bool Normalize(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var changed = false;
        foreach (var priorities in state.EmpirePriorities)
        {
            changed |= Normalize(priorities);
        }

        return changed;
    }

    public static bool Normalize(EmpirePriority priorities)
    {
        ArgumentNullException.ThrowIfNull(priorities);

        var original = (
            priorities.IndustryWeight,
            priorities.ResearchWeight,
            priorities.MilitaryWeight,
            priorities.ExpansionWeight);
        var originalTotal = (long)priorities.IndustryWeight
                            + priorities.ResearchWeight
                            + priorities.MilitaryWeight
                            + priorities.ExpansionWeight;
        if (originalTotal != TotalWeight
            || priorities.IndustryWeight < 0
            || priorities.ResearchWeight < 0
            || priorities.MilitaryWeight < 0
            || priorities.ExpansionWeight < 0)
        {
            return false;
        }

        var militaryWeight = Math.Max(0, priorities.MilitaryWeight);
        var expansionWeight = Math.Max(0, priorities.ExpansionWeight);
        var activeTotal = (long)militaryWeight + expansionWeight;

        priorities.IndustryWeight = 0;
        priorities.ResearchWeight = 0;
        priorities.MilitaryWeight = activeTotal == 0
            ? TotalWeight / 2
            : Math.Clamp(
                (int)Math.Round(
                    militaryWeight * (decimal)TotalWeight / activeTotal,
                    MidpointRounding.AwayFromZero),
                0,
                TotalWeight);
        priorities.ExpansionWeight = TotalWeight - priorities.MilitaryWeight;

        return original != (
            priorities.IndustryWeight,
            priorities.ResearchWeight,
            priorities.MilitaryWeight,
            priorities.ExpansionWeight);
    }

    internal static void Validate(EmpirePriority priorities)
    {
        ArgumentNullException.ThrowIfNull(priorities);
        var weights = new[]
        {
            priorities.IndustryWeight,
            priorities.ResearchWeight,
            priorities.MilitaryWeight,
            priorities.ExpansionWeight
        };

        if (weights.Any(weight => weight < 0))
        {
            throw new InvalidOperationException("Priority weights cannot be negative.");
        }

        if (weights.Sum(weight => (long)weight) != TotalWeight)
        {
            throw new InvalidOperationException($"Priority weights must total {TotalWeight}.");
        }

        if (priorities.IndustryWeight != 0 || priorities.ResearchWeight != 0)
        {
            throw new InvalidOperationException("Development and Innovation priorities are locked at zero until their programmes are active.");
        }
    }
}
