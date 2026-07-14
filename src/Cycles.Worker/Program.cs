using Cycles.Core;
using Cycles.Infrastructure.SqlServer;
using Cycles.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
var configuredSqlConnectionString = builder.Configuration.GetConnectionString("Cycles")
    ?? builder.Configuration["Cycles:SqlConnectionString"]
    ?? Environment.GetEnvironmentVariable("CYCLES_SQL_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(configuredSqlConnectionString))
{
    throw new InvalidOperationException("Cycles.Worker requires a Cycles SQL connection string. Configure ConnectionStrings:Cycles or CYCLES_SQL_CONNECTION_STRING.");
}
Func<GameState>? developmentSeedFactory = builder.Environment.IsDevelopment()
    ? () => GameSeeder.CreateCuratedColdStart()
    : null;

builder.Services.AddSingleton<IGameStateStore>(new SqlServerGameStateStore(configuredSqlConnectionString, developmentSeedFactory));
builder.Services.Configure<TickWorkerOptions>(builder.Configuration.GetSection("Cycles:Worker"));
builder.Services.AddHostedService<TickWorker>();
builder.Services.AddSingleton(TimeProvider.System);

await builder.Build().RunAsync();
