namespace Cycles.Tests;

public sealed class TurnResolutionRouteContractTests
{
    [Fact]
    public void Selected_game_exposes_antiforgery_protected_self_paced_resolution()
    {
        var program = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "OnlineSource",
            "Api",
            "Program.cs"));

        var start = program.IndexOf(
            "selectedGameRoutes.MapPost(\"/turns/resolve\"",
            StringComparison.Ordinal);
        var end = program.IndexOf(
            "selectedGameRoutes.MapPost(\"/tutorial/start-fresh\"",
            start,
            StringComparison.Ordinal);

        Assert.True(start >= 0);
        Assert.True(end > start);
        var route = program[start..end];
        Assert.Contains("SelfPacedTurnResolutionRequest request", route, StringComparison.Ordinal);
        Assert.Contains("ResolveSelfPacedTurn", route, StringComparison.Ordinal);
        Assert.Contains(".RequireCyclesAntiforgery();", route, StringComparison.Ordinal);
        Assert.Contains(
            "ExplicitCycleResolutionPolicy.SelfPacedParticipant",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "ExplicitCycleResolutionPolicy.TutorialJourney",
            program,
            StringComparison.Ordinal);
    }
}
