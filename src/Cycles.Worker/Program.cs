using Cycles.Application;
using Cycles.Core;
using Cycles.Infrastructure.SqlServer;
using Cycles.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
GameProfileCatalogue.EnsureValid();
var configuredSqlConnectionString = builder.Configuration.GetConnectionString("Cycles")
    ?? builder.Configuration["Cycles:SqlConnectionString"]
    ?? Environment.GetEnvironmentVariable("CYCLES_SQL_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(configuredSqlConnectionString))
{
    throw new InvalidOperationException("Cycles.Worker requires a Cycles SQL connection string. Configure ConnectionStrings:Cycles or CYCLES_SQL_CONNECTION_STRING.");
}
builder.Services.AddSingleton(new SqlServerGameStateStore(configuredSqlConnectionString));
builder.Services.AddSingleton<IDueCycleQuery>(services =>
    services.GetRequiredService<SqlServerGameStateStore>());
builder.Services.AddSingleton<IWorkerScheduleStatusQuery>(services =>
    services.GetRequiredService<SqlServerGameStateStore>());
builder.Services.AddSingleton<ICycleResolutionStore>(services =>
    services.GetRequiredService<SqlServerGameStateStore>());
builder.Services.Configure<TickWorkerOptions>(builder.Configuration.GetSection("Cycles:Worker"));
builder.Services.AddHostedService<TickWorker>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<WorkerHealthState>();

var app = builder.Build();
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", (
    WorkerHealthState health,
    IOptions<TickWorkerOptions> options,
    TimeProvider timeProvider) =>
{
    var readiness = WorkerHealthEvaluator.EvaluateReadiness(
        health.Snapshot,
        options.Value,
        timeProvider.GetUtcNow());
    return Results.Json(
        readiness.Report,
        statusCode: readiness.IsReady
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable);
});

await app.RunAsync();
