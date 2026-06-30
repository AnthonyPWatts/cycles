using Cycles.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cycles.Tests;

public sealed class ApiOrderBoundaryTests
{
    private static readonly IServiceProvider ResultServices = CreateResultServices();

    [Fact]
    public async Task MoveOrderEndpointAcceptsAdjacentActiveFleet()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        var store = new InMemoryGameStateStore(state);

        var result = ApiOrderEndpoints.SubmitMove(
            new MoveFleetRequest(fleet.FleetId, destination.SystemId),
            store,
            TestState.Now);

        var response = await ExecuteAsync(result);
        var order = Assert.Single(state.FleetOrders);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(FleetOrderStatus.Pending, order.Status);
        Assert.Equal(FleetOrderType.MoveFleet, order.OrderType);
        Assert.Equal(fleet.FleetId, order.FleetId);
        Assert.Equal(destination.SystemId, order.TargetSystemId);
        Assert.Equal(1, order.ExecuteAfterTick);
    }

    [Fact]
    public async Task MoveOrderEndpointRejectsNonAdjacentTarget()
    {
        var state = TestState.CreateMovementState(linkSystems: false);
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        var store = new InMemoryGameStateStore(state);

        var result = ApiOrderEndpoints.SubmitMove(
            new MoveFleetRequest(fleet.FleetId, destination.SystemId),
            store,
            TestState.Now);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("adjacent linked system", response.Body, StringComparison.Ordinal);
        Assert.Empty(state.FleetOrders);
    }

    [Fact]
    public async Task MoveOrderEndpointRejectsUnknownFleet()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        var store = new InMemoryGameStateStore(state);

        var result = ApiOrderEndpoints.SubmitMove(
            new MoveFleetRequest(Guid.NewGuid(), destination.SystemId),
            store,
            TestState.Now);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("Fleet does not exist", response.Body, StringComparison.Ordinal);
        Assert.Empty(state.FleetOrders);
    }

    [Fact]
    public async Task AttackOrderEndpointRejectsOwnEmpire()
    {
        var state = TestState.CreateSingleEmpireState();
        var fleet = Assert.Single(state.Fleets);
        var empire = Assert.Single(state.Empires);
        var store = new InMemoryGameStateStore(state);

        var result = ApiOrderEndpoints.SubmitAttack(
            new AttackFleetRequest(fleet.FleetId, empire.EmpireId),
            store,
            TestState.Now);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("cannot attack its own empire", response.Body, StringComparison.Ordinal);
        Assert.Empty(state.FleetOrders);
    }

    [Fact]
    public async Task AttackOrderEndpointRejectsUnknownTargetEmpire()
    {
        var state = TestState.CreateSingleEmpireState();
        var fleet = Assert.Single(state.Fleets);
        var store = new InMemoryGameStateStore(state);

        var result = ApiOrderEndpoints.SubmitAttack(
            new AttackFleetRequest(fleet.FleetId, Guid.NewGuid()),
            store,
            TestState.Now);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("Target empire does not exist", response.Body, StringComparison.Ordinal);
        Assert.Empty(state.FleetOrders);
    }

    [Fact]
    public async Task CancelOrderEndpointCancelsPendingOrderForOwningEmpire()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var empire = Assert.Single(state.Empires);
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        var order = OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);
        var store = new InMemoryGameStateStore(state);

        var result = ApiOrderEndpoints.Cancel(
            new CancelFleetOrderRequest(order.FleetOrderId, empire.EmpireId),
            store,
            TestState.Now);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(FleetOrderStatus.Cancelled, order.Status);
        Assert.Single(state.Events, item => item.EventType == EventType.OrderCancelled);
    }

    [Fact]
    public async Task CancelOrderEndpointRejectsProcessedOrder()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var empire = Assert.Single(state.Empires);
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        var order = OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);
        order.Status = FleetOrderStatus.Processed;
        order.ProcessedTick = 1;
        var store = new InMemoryGameStateStore(state);

        var result = ApiOrderEndpoints.Cancel(
            new CancelFleetOrderRequest(order.FleetOrderId, empire.EmpireId),
            store,
            TestState.Now);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("Only pending orders", response.Body, StringComparison.Ordinal);
        Assert.Equal(FleetOrderStatus.Processed, order.Status);
    }

    [Fact]
    public async Task PriorityEndpointRejectsWeightsThatDoNotTotalOneHundred()
    {
        var state = TestState.CreateSingleEmpireState();
        var empire = Assert.Single(state.Empires);
        var store = new InMemoryGameStateStore(state);

        var result = ApiOrderEndpoints.UpdatePriorities(
            new PriorityRequest(empire.EmpireId, 25, 25, 25, 20),
            store,
            TestState.Now);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("must total 100", response.Body, StringComparison.Ordinal);
        Assert.Empty(state.Events);
    }

    private static async Task<(int StatusCode, string Body)> ExecuteAsync(IResult result)
    {
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.RequestServices = ResultServices;
        context.Response.Body = body;

        await result.ExecuteAsync(context);

        body.Position = 0;
        using var reader = new StreamReader(body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private static IServiceProvider CreateResultServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        });

        return services.BuildServiceProvider();
    }

    private sealed class InMemoryGameStateStore(GameState state) : IGameStateStore
    {
        public string Description => "In-memory test state";

        public GameState LoadOrCreate() => state;

        public T Update<T>(Func<GameState, T> update) => update(state);

        public void Replace(GameState replacement)
        {
            throw new NotSupportedException("Replacement is not needed for API boundary tests.");
        }
    }
}
