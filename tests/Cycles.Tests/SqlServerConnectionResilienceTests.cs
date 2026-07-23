using Cycles.Infrastructure.SqlServer;

namespace Cycles.Tests;

public sealed class SqlServerConnectionResilienceTests
{
    [Fact]
    public void Created_connections_retry_serverless_resume_failures()
    {
        using var connection = SqlServerGameStateStore.CreateConnection(
            "Server=localhost;Database=CyclesDb;User Id=test;Password=test;Encrypt=False");

        Assert.NotNull(connection.RetryLogicProvider);
        Assert.Equal(3, connection.RetryLogicProvider.RetryLogic.NumberOfTries);
        Assert.Contains(-2, SqlServerGameStateStore.TransientConnectionErrorNumbers);
        Assert.Contains(40613, SqlServerGameStateStore.TransientConnectionErrorNumbers);
    }
}
