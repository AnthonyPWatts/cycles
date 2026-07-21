using Cycles.Application;
using Cycles.Core;

namespace Cycles.Tests;

public sealed class GamesHomeProjectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Games_are_one_list_with_current_training_first_then_player_deadlines()
    {
        var futureLater = ActiveGame("Future later", Now.AddMinutes(45));
        var training = ActiveGame("Training", nextTickAt: null, purpose: GamePurpose.Training);
        var overdue = ActiveGame("Overdue", Now.AddMinutes(-5));
        var futureSoon = ActiveGame("Future soon", Now.AddMinutes(10));

        var home = GamesHomeProjection.Create(
            new GameCataloguePage([futureLater, training, overdue, futureSoon], nextCursor: null));

        Assert.Collection(
            home.Games,
            item => Assert.Equal("Training", item.Game.GameName),
            item => Assert.Equal("Overdue", item.Game.GameName),
            item => Assert.Equal("Future soon", item.Game.GameName),
            item => Assert.Equal("Future later", item.Game.GameName));
        Assert.Equal(home.Games.Count, home.Games.Select(item => item.Game.GameId).Distinct().Count());
    }

    [Fact]
    public void Deadline_ties_and_no_deadline_states_have_deterministic_fallbacks()
    {
        var deadlineZulu = ActiveGame("Zulu deadline", Now.AddMinutes(10));
        var deadlineAlpha = ActiveGame("Alpha deadline", Now.AddMinutes(10));
        var recovery = ActiveGame("Recovery", nextTickAt: null) with
        {
            OperationalCycleStatus = CycleStatus.RecoveryRequired,
            TurnStage = null
        };
        var selfPaced = ActiveGame("Self-paced", nextTickAt: null);
        var resolving = ActiveGame("Resolving", Now.AddMinutes(-30)) with
        {
            TurnStage = TurnResolutionStage.Resolving
        };
        var waiting = Game("Waiting", GameLifecycleStatus.Forming, operationalCycleStatus: null);
        var complete = Game("Complete", GameLifecycleStatus.Completed, CycleStatus.Completed);
        var withdrawn = Game("Withdrawn", GameLifecycleStatus.Active, CycleStatus.Active) with
        {
            EnrolmentStatus = GameEnrolmentStatus.Withdrawn
        };

        var home = GamesHomeProjection.Create(
            new GameCataloguePage(
                [withdrawn, complete, waiting, resolving, selfPaced, recovery, deadlineZulu, deadlineAlpha],
                nextCursor: null));

        Assert.Equal(
            ["Alpha deadline", "Zulu deadline", "Recovery", "Self-paced", "Resolving", "Waiting", "Complete", "Withdrawn"],
            home.Games.Select(item => item.Game.GameName));
        Assert.Equal(GamesHomeAction.Continue, home.Games.Single(item => item.Game.GameName == "Self-paced").Action);
        Assert.Equal(GamesHomeAction.Observe, home.Games.Single(item => item.Game.GameName == "Recovery").Action);
        Assert.Null(home.Games.Single(item => item.Game.GameName == "Resolving").CommandDeadline);
        Assert.Equal(Now.AddMinutes(10), home.Games.Single(item => item.Game.GameName == "Alpha deadline").CommandDeadline);
        Assert.Equal(GamesHomeAction.EnterLobby, home.Games.Single(item => item.Game.GameName == "Waiting").Action);
        Assert.Equal(GamesHomeAction.Review, home.Games.Single(item => item.Game.GameName == "Complete").Action);
        Assert.Equal(GamesHomeAction.Review, home.Games.Single(item => item.Game.GameName == "Withdrawn").Action);
    }

    [Fact]
    public void Completed_training_is_a_retained_record_not_an_in_progress_tutorial()
    {
        var standard = ActiveGame("Standard", nextTickAt: null);
        var completedTraining = Game(
            "Completed Training",
            GameLifecycleStatus.Completed,
            CycleStatus.Completed,
            GamePurpose.Training);

        var home = GamesHomeProjection.Create(
            new GameCataloguePage([completedTraining, standard], nextCursor: null));

        Assert.Equal(["Standard", "Completed Training"], home.Games.Select(item => item.Game.GameName));
    }

    [Fact]
    public void Zero_memberships_is_an_intentional_empty_home()
    {
        var home = GamesHomeProjection.Create(
            new GameCataloguePage([], nextCursor: null));

        Assert.Empty(home.Games);
        Assert.Empty(home.Tutorials);
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
}
