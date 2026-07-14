using Cycles.Core;

namespace Cycles.Tests;

public sealed class StrategicPriorityPolicyTests
{
    [Fact]
    public void LegacyAllocationsMoveInactivePointsIntoTheExistingActiveRatio()
    {
        var priorities = new EmpirePriority
        {
            IndustryWeight = 30,
            ResearchWeight = 25,
            MilitaryWeight = 30,
            ExpansionWeight = 15
        };

        var changed = StrategicPriorityPolicy.Normalize(priorities);

        Assert.True(changed);
        Assert.Equal(0, priorities.IndustryWeight);
        Assert.Equal(0, priorities.ResearchWeight);
        Assert.Equal(67, priorities.MilitaryWeight);
        Assert.Equal(33, priorities.ExpansionWeight);
    }

    [Fact]
    public void EmptyActiveAllocationFallsBackToAnEvenSplit()
    {
        var priorities = new EmpirePriority
        {
            IndustryWeight = 50,
            ResearchWeight = 50,
            MilitaryWeight = 0,
            ExpansionWeight = 0
        };

        StrategicPriorityPolicy.Normalize(priorities);

        Assert.Equal(0, priorities.IndustryWeight);
        Assert.Equal(0, priorities.ResearchWeight);
        Assert.Equal(50, priorities.MilitaryWeight);
        Assert.Equal(50, priorities.ExpansionWeight);
    }

    [Fact]
    public void NormalisationIsIdempotent()
    {
        var priorities = new EmpirePriority();

        Assert.False(StrategicPriorityPolicy.Normalize(priorities));
        Assert.False(StrategicPriorityPolicy.Normalize(priorities));
    }

    [Fact]
    public void InvalidLegacyAllocationIsNotSilentlyRepaired()
    {
        var priorities = new EmpirePriority
        {
            IndustryWeight = -10,
            ResearchWeight = 0,
            MilitaryWeight = 10,
            ExpansionWeight = 0
        };

        Assert.False(StrategicPriorityPolicy.Normalize(priorities));
        Assert.Equal((-10, 0, 10, 0), (
            priorities.IndustryWeight,
            priorities.ResearchWeight,
            priorities.MilitaryWeight,
            priorities.ExpansionWeight));
    }
}
