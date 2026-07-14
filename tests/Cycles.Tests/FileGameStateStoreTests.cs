using Cycles.Core;
using System.Text.Json;

namespace Cycles.Tests;

public sealed class FileGameStateStoreTests
{
    [Fact]
    public void LoadOrCreateNormalisesAndPersistsLegacyPriorities()
    {
        var statePath = Path.Combine(Path.GetTempPath(), $"cycles-priority-{Guid.NewGuid():N}.json");
        try
        {
            var state = TestState.CreateSingleEmpireState();
            var priorities = Assert.Single(state.EmpirePriorities);
            priorities.IndustryWeight = 30;
            priorities.ResearchWeight = 25;
            priorities.MilitaryWeight = 30;
            priorities.ExpansionWeight = 15;
            File.WriteAllText(statePath, JsonSerializer.Serialize(state, GameStateJson.Options));

            var loaded = new FileGameStateStore(statePath).LoadOrCreate();
            var loadedPriorities = Assert.Single(loaded.EmpirePriorities);
            var persisted = JsonSerializer.Deserialize<GameState>(File.ReadAllText(statePath), GameStateJson.Options)!;
            var persistedPriorities = Assert.Single(persisted.EmpirePriorities);

            Assert.Equal((0, 0, 67, 33), Weights(loadedPriorities));
            Assert.Equal((0, 0, 67, 33), Weights(persistedPriorities));
        }
        finally
        {
            File.Delete(statePath);
            File.Delete($"{statePath}.lock");
            File.Delete($"{statePath}.tmp");
        }
    }

    private static (int Industry, int Research, int Military, int Expansion) Weights(EmpirePriority priorities) =>
        (priorities.IndustryWeight, priorities.ResearchWeight, priorities.MilitaryWeight, priorities.ExpansionWeight);
}
