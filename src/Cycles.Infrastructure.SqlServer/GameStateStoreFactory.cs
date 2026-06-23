using Cycles.Core;

namespace Cycles.Infrastructure.SqlServer;

public static class GameStateStoreFactory
{
    public const string SqlServerPrefix = "sqlserver:";

    public static IGameStateStore Create(
        string filePathOrStoreSpecifier,
        string? sqlConnectionString = null,
        Func<GameState>? seedFactory = null)
    {
        if (!string.IsNullOrWhiteSpace(sqlConnectionString))
        {
            return new SqlServerGameStateStore(sqlConnectionString, seedFactory);
        }

        if (TryParseSqlServerSpecifier(filePathOrStoreSpecifier, out var parsedConnectionString))
        {
            return new SqlServerGameStateStore(parsedConnectionString, seedFactory);
        }

        return new FileGameStateStore(filePathOrStoreSpecifier, seedFactory);
    }

    public static bool TryParseSqlServerSpecifier(string value, out string connectionString)
    {
        if (value.StartsWith(SqlServerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            connectionString = value[SqlServerPrefix.Length..].Trim();
            return connectionString.Length > 0;
        }

        connectionString = "";
        return false;
    }
}
