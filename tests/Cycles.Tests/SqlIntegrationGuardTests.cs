namespace Cycles.Tests;

public sealed class SqlIntegrationGuardTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("yes")]
    public void Required_switch_recognises_enabled_values(string value)
    {
        Assert.True(SqlIntegrationGuard.IsRequired(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("no")]
    public void Required_switch_rejects_disabled_values(string? value)
    {
        Assert.False(SqlIntegrationGuard.IsRequired(value));
    }

    [Fact]
    public void Missing_connection_is_rejected_when_sql_integration_is_required()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => SqlIntegrationGuard.EnsureConfiguration(null, "1"));

        Assert.Contains(SqlIntegrationGuard.ConnectionStringEnvironmentVariable, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "0")]
    [InlineData("Server=localhost;Database=CyclesIntegration;", "1")]
    public void Optional_or_configured_sql_integration_is_accepted(string? connectionString, string? requiredValue)
    {
        SqlIntegrationGuard.EnsureConfiguration(connectionString, requiredValue);
    }
}
