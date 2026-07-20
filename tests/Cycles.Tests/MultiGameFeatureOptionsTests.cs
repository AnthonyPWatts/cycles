using Microsoft.Extensions.Configuration;

namespace Cycles.Tests;

public sealed class MultiGameFeatureOptionsTests
{
    private static readonly Guid PilotPlayerId = Guid.Parse("76aa44d2-aa7c-4e10-8a40-4af1ac94aa01");

    [Fact]
    public void Training_is_exposed_only_to_an_allow_listed_player()
    {
        var options = Read(new Dictionary<string, string?>
        {
            ["Cycles:Features:GamesAccountShell:Enabled"] = "true",
            ["Cycles:Features:TrainingGames:Enabled"] = "true",
            ["Cycles:Features:TrainingGames:PilotPlayerIds:0"] = PilotPlayerId.ToString()
        });

        Assert.True(options.TrainingGames.Includes(PilotPlayerId));
        Assert.False(options.TrainingGames.Includes(Guid.NewGuid()));
    }

    [Fact]
    public void Training_cannot_be_enabled_without_the_account_shell()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Read(
            new Dictionary<string, string?>
            {
                ["Cycles:Features:TrainingGames:Enabled"] = "true",
                ["Cycles:Features:TrainingGames:PilotPlayerIds:0"] = PilotPlayerId.ToString()
            }));

        Assert.Contains("requires GamesAccountShell", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_pilot_identifier_fails_startup_configuration()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Read(
            new Dictionary<string, string?>
            {
                ["Cycles:Features:GamesAccountShell:Enabled"] = "true",
                ["Cycles:Features:TrainingGames:PilotPlayerIds:0"] = "not-a-player-id"
            }));

        Assert.Contains("invalid Player identifier", exception.Message, StringComparison.Ordinal);
    }

    private static MultiGameFeatureOptions Read(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return MultiGameFeatureOptions.Read(configuration);
    }
}
