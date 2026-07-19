namespace Cycles.Tests;

internal static class SqlIntegrationGuard
{
    public const string CategoryName = "Category";
    public const string CategoryValue = "SqlIntegration";
    public const string ConnectionStringEnvironmentVariable = "CYCLES_SQL_INTEGRATION_CONNECTION_STRING";
    public const string RequiredEnvironmentVariable = "CYCLES_REQUIRE_SQL_INTEGRATION";

    public static string? GetConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        EnsureConfiguration(
            connectionString,
            Environment.GetEnvironmentVariable(RequiredEnvironmentVariable));
        return string.IsNullOrWhiteSpace(connectionString) ? null : connectionString;
    }

    internal static void EnsureConfiguration(string? connectionString, string? requiredValue)
    {
        if (IsRequired(requiredValue) && string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"SQL Server integration is required, but {ConnectionStringEnvironmentVariable} is not configured.");
        }
    }

    internal static bool IsRequired(string? value) =>
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}
