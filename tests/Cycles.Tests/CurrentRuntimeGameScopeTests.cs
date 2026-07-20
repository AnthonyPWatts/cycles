using Cycles.Core;

namespace Cycles.Tests;

public sealed class CurrentRuntimeGameScopeTests
{
    [Fact]
    public void Operational_import_accepts_the_deterministic_legacy_game()
    {
        var state = TestState.CreateSingleEmpireState();
        LegacyGameFoundation.Apply(state);

        CurrentRuntimeGameScope.EnsureSupportedForOperationalImport(state);
    }

    [Fact]
    public void Operational_import_uses_the_fixed_game_identity_without_freezing_authoritative_metadata()
    {
        var state = TestState.CreateSingleEmpireState();
        LegacyGameFoundation.Apply(state);
        Assert.Single(state.Games).Name = "Renamed campaign";

        var validation = GameStateTransfer.Validate(state);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));

        CurrentRuntimeGameScope.EnsureSupportedForOperationalImport(state);
    }

    [Fact]
    public void Operational_import_rejects_an_additional_game_before_SQL_is_touched()
    {
        var state = TestState.CreateSingleEmpireState();
        LegacyGameFoundation.Apply(state);
        state.Games.Add(new Game
        {
            Name = "Training",
            Purpose = GamePurpose.Training,
            Status = GameLifecycleStatus.Forming,
            Visibility = GameVisibility.Private,
            CreationSource = GameCreationSource.Operator,
            GamePolicyKey = "training-v1",
            GamePolicyVersion = 1,
            GamePolicyContentHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            PolicyProvenanceStatus = ProvenanceStatus.Verified,
            CreatedAt = TestState.Now
        });

        var validation = GameStateTransfer.Validate(state);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CurrentRuntimeGameScope.EnsureSupportedForOperationalImport(state));

        Assert.Contains("can currently import only", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Game provisioning", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Operational_import_rejects_foundation_rows_outside_the_legacy_game()
    {
        var state = TestState.CreateSingleEmpireState();
        LegacyGameFoundation.Apply(state);
        state.GameLifecycleEvents[0].GameId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() =>
            CurrentRuntimeGameScope.EnsureSupportedForOperationalImport(state));
    }
}
