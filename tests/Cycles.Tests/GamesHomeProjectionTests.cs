using Cycles.Application;
using Cycles.Core;

namespace Cycles.Tests;

public sealed class GamesHomeProjectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Attention_is_server_ranked_limited_and_keeps_the_total()
    {
        var recovery = ActiveGame("Recovery", nextTickAt: null) with
        {
            OperationalCycleStatus = CycleStatus.RecoveryRequired
        };
        var deadline = ActiveGame("Deadline", Now.AddMinutes(15));
        var started = ActiveGame("Started", nextTickAt: null) with
        {
            FirstStartedAt = Now.AddHours(-1)
        };
        var training = ActiveGame("Training", nextTickAt: null, purpose: GamePurpose.Training);

        var home = GamesHomeProjection.Create(
            new GameCataloguePage([training, started, deadline, recovery], nextCursor: null),
            Now);

        Assert.Equal(4, home.TotalAttentionCount);
        Assert.Equal(3, home.NeedsAttention.Count);
        Assert.Collection(
            home.NeedsAttention,
            item => AssertAttention(item, 1, GamesHomeAttentionReason.RecoveryRequired),
            item => AssertAttention(item, 2, GamesHomeAttentionReason.CommandsCloseSoon),
            item => AssertAttention(item, 3, GamesHomeAttentionReason.GameStarted));
        Assert.Equal(4, home.ActiveGames.Single(item => item.Game.GameName == "Training").AttentionRank);
    }

    [Fact]
    public void Home_groups_games_and_derives_contextual_actions_without_client_policy()
    {
        var active = ActiveGame("Active", Now.AddMinutes(30));
        var waiting = Game("Waiting", GameLifecycleStatus.Forming, operationalCycleStatus: null);
        var complete = Game("Complete", GameLifecycleStatus.Completed, CycleStatus.Completed);
        var withdrawn = Game("Withdrawn", GameLifecycleStatus.Active, CycleStatus.Active) with
        {
            EnrolmentStatus = GameEnrolmentStatus.Withdrawn
        };

        var home = GamesHomeProjection.Create(
            new GameCataloguePage([complete, waiting, withdrawn, active], nextCursor: null),
            Now);

        Assert.Equal(GamesHomeAction.Continue, home.ActiveGames.Single(item => item.Game.GameName == "Active").Action);
        Assert.Equal(GamesHomeAction.Review, home.ActiveGames.Single(item => item.Game.GameName == "Withdrawn").Action);
        Assert.Equal(GamesHomeAction.EnterLobby, Assert.Single(home.WaitingGames).Action);
        Assert.Equal(GamesHomeAction.Review, Assert.Single(home.CompletedGames).Action);
        Assert.DoesNotContain(home.NeedsAttention, item => item.Game.GameName == "Withdrawn");
    }

    [Fact]
    public void Zero_memberships_is_an_intentional_empty_home()
    {
        var home = GamesHomeProjection.Create(
            new GameCataloguePage([], nextCursor: null),
            Now);

        Assert.Empty(home.NeedsAttention);
        Assert.Empty(home.ActiveGames);
        Assert.Empty(home.WaitingGames);
        Assert.Empty(home.CompletedGames);
        Assert.Equal(0, home.TotalAttentionCount);
        Assert.False(home.HasMore);
    }

    private static GameCatalogueItem ActiveGame(
        string name,
        DateTimeOffset? nextTickAt,
        GamePurpose purpose = GamePurpose.Standard) =>
        Game(name, GameLifecycleStatus.Active, CycleStatus.Active, purpose) with
        {
            FirstStartedAt = Now.AddDays(-2),
            TickLengthMinutes = 60,
            NextTickAt = nextTickAt
        };

    private static GameCatalogueItem Game(
        string name,
        GameLifecycleStatus lifecycle,
        CycleStatus? operationalCycleStatus,
        GamePurpose purpose = GamePurpose.Standard) =>
        new(
            Guid.NewGuid(),
            name,
            purpose,
            lifecycle,
            GameVisibility.Private,
            Guid.NewGuid(),
            GameEnrolmentStatus.Enrolled,
            Now.AddDays(-1),
            operationalCycleStatus is null ? null : Guid.NewGuid(),
            operationalCycleStatus,
            operationalCycleStatus is null ? null : 4,
            operationalCycleStatus == CycleStatus.Active ? TurnResolutionStage.CommandOpen : null,
            Now.AddDays(-10));

    private static void AssertAttention(
        GamesHomeItem item,
        int rank,
        GamesHomeAttentionReason reason)
    {
        Assert.Equal(rank, item.AttentionRank);
        Assert.Equal(reason, item.AttentionReason);
    }
}
