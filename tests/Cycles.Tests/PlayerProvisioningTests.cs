using Cycles.Core;

namespace Cycles.Tests;

public sealed class PlayerProvisioningTests
{
    [Fact]
    public void New_players_receive_distinct_personal_admiral_names()
    {
        var state = TestState.CreateSingleEmpireState();
        LegacyGameFoundation.Apply(state);
        var cycle = state.GetActiveCycle()!;
        var firstPlayer = AddPlayer(state, "player-1");
        var secondPlayer = AddPlayer(state, "player-2");

        var firstEmpire = PlayerProvisioning.AddEmpireForPlayer(state, cycle, firstPlayer, null, TestState.Now);
        var secondEmpire = PlayerProvisioning.AddEmpireForPlayer(state, cycle, secondPlayer, null, TestState.Now);

        var firstAdmiral = Assert.Single(state.Admirals, item => item.EmpireId == firstEmpire.EmpireId);
        var secondAdmiral = Assert.Single(state.Admirals, item => item.EmpireId == secondEmpire.EmpireId);
        Assert.False(string.Equals(firstAdmiral.AdmiralName, secondAdmiral.AdmiralName, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(firstPlayer.Username, firstAdmiral.AdmiralName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secondPlayer.Username, secondAdmiral.AdmiralName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Legacy_username_based_admiral_name_is_replaced_once()
    {
        var state = TestState.CreateSingleEmpireState();
        LegacyGameFoundation.Apply(state);
        var cycle = state.GetActiveCycle()!;
        var player = AddPlayer(state, "player-1");
        var empire = PlayerProvisioning.AddEmpireForPlayer(state, cycle, player, null, TestState.Now);
        var admiral = Assert.Single(state.Admirals, item => item.EmpireId == empire.EmpireId);
        admiral.AdmiralName = "player-1 Vanguard";

        PlayerProvisioning.RepairLegacyStartingAdmiralName(state, empire, player, TestState.Now.AddMinutes(1));
        var repairedName = admiral.AdmiralName;
        PlayerProvisioning.RepairLegacyStartingAdmiralName(state, empire, player, TestState.Now.AddMinutes(2));

        Assert.False(string.Equals("player-1 Vanguard", repairedName, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(repairedName, admiral.AdmiralName);
        Assert.Equal(TestState.Now.AddMinutes(1), admiral.UpdatedAt);
    }

    private static Player AddPlayer(GameState state, string username)
    {
        var player = new Player
        {
            Username = username,
            Email = $"{username}@example.test",
            CreatedAt = TestState.Now,
            Status = PlayerStatus.Active
        };
        state.Players.Add(player);
        return player;
    }
}
