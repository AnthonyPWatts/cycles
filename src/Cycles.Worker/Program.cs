using Cycles.Infrastructure.SqlServer;
using Cycles.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
var configuredStatePath = builder.Configuration["Cycles:StatePath"]
    ?? Environment.GetEnvironmentVariable("CYCLES_STATE_PATH")
    ?? Path.Combine(builder.Environment.ContentRootPath, "data", "cycles-state.json");
var configuredSqlConnectionString = builder.Configuration.GetConnectionString("Cycles")
    ?? builder.Configuration["Cycles:SqlConnectionString"]
    ?? Environment.GetEnvironmentVariable("CYCLES_SQL_CONNECTION_STRING");

builder.Services.AddSingleton(GameStateStoreFactory.Create(configuredStatePath, configuredSqlConnectionString));
builder.Services.Configure<TickWorkerOptions>(builder.Configuration.GetSection("Cycles:Worker"));
builder.Services.AddHostedService<TickWorker>();
builder.Services.AddSingleton(TimeProvider.System);

await builder.Build().RunAsync();
