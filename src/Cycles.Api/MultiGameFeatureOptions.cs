public sealed record PlayerFeatureAudience(
    bool Enabled,
    IReadOnlySet<Guid> PilotPlayerIds)
{
    public bool Includes(Guid playerId) => Enabled && PilotPlayerIds.Contains(playerId);
}

public sealed record MultiGameFeatureOptions(
    PlayerFeatureAudience GamesAccountShell,
    PlayerFeatureAudience TrainingGames,
    PlayerFeatureAudience ManualGameEnrolment,
    bool MultiCycleBatchEnabled)
{
    private const string SectionName = "Cycles:Features";

    public static MultiGameFeatureOptions Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new MultiGameFeatureOptions(
            ReadAudience(configuration, "GamesAccountShell"),
            ReadAudience(configuration, "TrainingGames"),
            ReadAudience(configuration, "ManualGameEnrolment"),
            configuration.GetValue<bool>($"{SectionName}:MultiCycleBatch:Enabled"));

        if (options.TrainingGames.Enabled && !options.GamesAccountShell.Enabled)
        {
            throw new InvalidOperationException(
                "Cycles:Features:TrainingGames requires GamesAccountShell to be enabled.");
        }
        if (options.ManualGameEnrolment.Enabled && !options.GamesAccountShell.Enabled)
        {
            throw new InvalidOperationException(
                "Cycles:Features:ManualGameEnrolment requires GamesAccountShell to be enabled.");
        }

        return options;
    }

    private static PlayerFeatureAudience ReadAudience(
        IConfiguration configuration,
        string featureName)
    {
        var path = $"{SectionName}:{featureName}";
        var ids = configuration.GetSection($"{path}:PilotPlayerIds").Get<string[]>() ?? [];
        var parsed = new HashSet<Guid>();
        foreach (var configuredId in ids)
        {
            if (!Guid.TryParse(configuredId, out var playerId) || playerId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    $"{path}:PilotPlayerIds contains invalid Player identifier '{configuredId}'.");
            }
            if (!parsed.Add(playerId))
            {
                throw new InvalidOperationException(
                    $"{path}:PilotPlayerIds contains duplicate Player identifier '{configuredId}'.");
            }
        }

        return new PlayerFeatureAudience(
            configuration.GetValue<bool>($"{path}:Enabled"),
            parsed);
    }
}
