using Cycles.Application;
using Cycles.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cycles.Tests;

public sealed class ApiOrderBoundaryTests
{
    private static readonly IServiceProvider ResultServices = CreateResultServices();

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 4)]
    public async Task MoveOrderEndpointReturnsProjectedTimingForAdjacentActiveFleet(
        int travelTicks,
        int expectedArrivalTick)
    {
        var state = TestState.CreateMovementState(linkSystems: true, travelTicks: travelTicks);
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        var game = new FocusedSelectedGameFixture(state);
        var httpContext = CreateAuthenticatedContext(state);

        var result = ApiOrderEndpoints.SubmitMove(
            new MoveFleetRequest(fleet.FleetId, destination.SystemId),
            httpContext,
            game.GameId,
            game.Requests,
            TestState.Now);

        var response = await ExecuteAsync(result);
        var order = Assert.Single(state.FleetOrders);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(FleetOrderStatus.Pending, order.Status);
        Assert.Equal(FleetOrderType.MoveFleet, order.OrderType);
        Assert.Equal(fleet.FleetId, order.FleetId);
        Assert.Equal(destination.SystemId, order.TargetSystemId);
        Assert.Equal(1, order.ExecuteAfterTick);
        using var document = JsonDocument.Parse(response.Body);
        var projection = document.RootElement.GetProperty("moveJourneyProjection");
        Assert.True(projection.GetProperty("routeAvailable").GetBoolean());
        Assert.Equal(travelTicks, projection.GetProperty("travelTicks").GetInt32());
        Assert.Equal(1, projection.GetProperty("dispatchTickNumber").GetInt32());
        Assert.Equal(expectedArrivalTick, projection.GetProperty("arrivalTickNumber").GetInt32());
    }

    [Fact]
    public async Task MoveOrderEndpointRejectsNonAdjacentTarget()
    {
        var state = TestState.CreateMovementState(linkSystems: false);
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        var game = new FocusedSelectedGameFixture(state);
        var httpContext = CreateAuthenticatedContext(state);

        var result = ApiOrderEndpoints.SubmitMove(
            new MoveFleetRequest(fleet.FleetId, destination.SystemId),
            httpContext,
            game.GameId,
            game.Requests,
            TestState.Now);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("\"code\":\"validationFailed\"", response.Body, StringComparison.Ordinal);
        Assert.Contains("adjacent linked system", response.Body, StringComparison.Ordinal);
        Assert.Empty(state.FleetOrders);
    }

    [Fact]
    public async Task MoveOrderEndpointRejectsUnknownFleet()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        var game = new FocusedSelectedGameFixture(state);
        var httpContext = CreateAuthenticatedContext(state);

        var result = ApiOrderEndpoints.SubmitMove(
            new MoveFleetRequest(Guid.NewGuid(), destination.SystemId),
            httpContext,
            game.GameId,
            game.Requests,
            TestState.Now);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Contains("\"code\":\"notFound\"", response.Body, StringComparison.Ordinal);
        Assert.Contains("Fleet does not exist", response.Body, StringComparison.Ordinal);
        Assert.Empty(state.FleetOrders);
    }

    [Fact]
    public async Task RecallOrderEndpointAcceptsOwnedOutboundFleetInTransit()
    {
        var state = TestState.CreateMovementState(linkSystems: true, travelTicks: 2);
        var cycle = state.GetActiveCycle()!;
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);
        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);
        var game = new FocusedSelectedGameFixture(state);

        var result = ApiOrderEndpoints.SubmitRecall(
            new RecallFleetRequest(fleet.FleetId),
            CreateAuthenticatedContext(state),
            game.GameId,
            game.Requests,
            TestState.Now.AddMinutes(1));

        var response = await ExecuteAsync(result);
        var recall = Assert.Single(state.FleetOrders, order => order.OrderType == FleetOrderType.RecallFleet);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(FleetOrderStatus.Pending, recall.Status);
        Assert.Equal(fleet.CurrentSystemId, recall.TargetSystemId);
        Assert.Equal(2, recall.ExecuteAfterTick);
    }

    [Fact]
    public async Task RecallOrderEndpointRejectsActiveFleet()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var fleet = Assert.Single(state.Fleets);
        var game = new FocusedSelectedGameFixture(state);

        var result = ApiOrderEndpoints.SubmitRecall(
            new RecallFleetRequest(fleet.FleetId),
            CreateAuthenticatedContext(state),
            game.GameId,
            game.Requests,
            TestState.Now);

        var response = await ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("outbound fleet in transit", response.Body, StringComparison.Ordinal);
        Assert.Empty(state.FleetOrders);
    }

    [Fact]
    public async Task AttackOrderEndpointRejectsOwnEmpire()
    {
        var state = TestState.CreateSingleEmpireState();
        var fleet = Assert.Single(state.Fleets);
        var empire = Assert.Single(state.Empires);
        var game = new FocusedSelectedGameFixture(state);
        var httpContext = CreateAuthenticatedContext(state);

        var result = ApiOrderEndpoints.SubmitAttack(
            new AttackFleetRequest(fleet.FleetId, empire.EmpireId),
            httpContext,
            game.GameId,
            game.Requests,
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
        var game = new FocusedSelectedGameFixture(state);
        var httpContext = CreateAuthenticatedContext(state);

        var result = ApiOrderEndpoints.SubmitAttack(
            new AttackFleetRequest(fleet.FleetId, Guid.NewGuid()),
            httpContext,
            game.GameId,
            game.Requests,
            TestState.Now);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("Target empire does not exist", response.Body, StringComparison.Ordinal);
        Assert.Empty(state.FleetOrders);
    }

    [Fact]
    public async Task AttackOrderEndpointRejectsWhenNoHostileFleetIsLocal()
    {
        var state = TestState.CreateSingleEmpireState();
        var fleet = Assert.Single(state.Fleets);
        var game = new FocusedSelectedGameFixture(state);

        var result = ApiOrderEndpoints.SubmitAttack(
            new AttackFleetRequest(fleet.FleetId, null),
            CreateAuthenticatedContext(state),
            game.GameId,
            game.Requests,
            TestState.Now);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("No hostile active fleet is present in this system", response.Body, StringComparison.Ordinal);
        Assert.Empty(state.FleetOrders);
    }

    [Fact]
    public async Task AttackOrderEndpointRejectsTargetFactionWithoutLocalActiveFleet()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 30, defenderShips: 20);
        var firstEmpire = state.Empires.Single(empire => empire.EmpireName == "First");
        var secondEmpire = state.Empires.Single(empire => empire.EmpireName == "Second");
        var attacker = state.Fleets.Single(fleet => fleet.EmpireId == firstEmpire.EmpireId);
        var defender = state.Fleets.Single(fleet => fleet.EmpireId == secondEmpire.EmpireId);
        defender.Status = FleetStatus.InTransit;
        var neutralFaction = new Faction
        {
            CycleId = attacker.CycleId,
            FactionName = "Local neutrals",
            Kind = FactionKind.Neutral,
            Status = FactionStatus.Active,
            CreatedAt = TestState.Now
        };
        state.Factions.Add(neutralFaction);
        state.Fleets.Add(new Fleet
        {
            CycleId = attacker.CycleId,
            FactionId = neutralFaction.FactionId,
            FleetName = "Local neutral fleet",
            CurrentSystemId = attacker.CurrentSystemId,
            ShipCount = 5,
            Status = FleetStatus.Active,
            CreatedAt = TestState.Now
        });
        var game = new FocusedSelectedGameFixture(state);

        var result = ApiOrderEndpoints.SubmitAttack(
            new AttackFleetRequest(attacker.FleetId, secondEmpire.EmpireId),
            CreateAuthenticatedContext(state, state.Players.Single(player => player.PlayerId == firstEmpire.PlayerId)),
            game.GameId,
            game.Requests,
            TestState.Now);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("No hostile active fleet is present in this system", response.Body, StringComparison.Ordinal);
        Assert.Empty(state.FleetOrders);
    }

    [Fact]
    public async Task AttackOrderEndpointRequiresAndAppliesConfirmedReplacement()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 30, defenderShips: 20);
        var firstEmpire = state.Empires.Single(empire => empire.EmpireName == "First");
        var secondEmpire = state.Empires.Single(empire => empire.EmpireName == "Second");
        var fleet = state.Fleets.Single(item => item.EmpireId == firstEmpire.EmpireId);
        var player = state.Players.Single(item => item.PlayerId == firstEmpire.PlayerId);
        var game = new FocusedSelectedGameFixture(state);
        var httpContext = CreateAuthenticatedContext(state, player);

        var firstResponse = await ExecuteAsync(ApiOrderEndpoints.SubmitAttack(
            new AttackFleetRequest(fleet.FleetId, secondEmpire.EmpireId),
            httpContext,
            game.GameId,
            game.Requests,
            TestState.Now));
        var pending = Assert.Single(state.FleetOrders);

        var conflictResponse = await ExecuteAsync(ApiOrderEndpoints.SubmitAttack(
            new AttackFleetRequest(fleet.FleetId, null),
            httpContext,
            game.GameId,
            game.Requests,
            TestState.Now.AddSeconds(1)));

        Assert.Equal(StatusCodes.Status200OK, firstResponse.StatusCode);
        Assert.Equal(StatusCodes.Status409Conflict, conflictResponse.StatusCode);
        Assert.Contains("stateConflict", conflictResponse.Body, StringComparison.Ordinal);
        Assert.Equal(FleetOrderStatus.Pending, pending.Status);

        var replacementResponse = await ExecuteAsync(ApiOrderEndpoints.SubmitAttack(
            new AttackFleetRequest(fleet.FleetId, null, pending.FleetOrderId),
            httpContext,
            game.GameId,
            game.Requests,
            TestState.Now.AddSeconds(2)));
        var replacement = state.FleetOrders.Single(order => order.Status == FleetOrderStatus.Pending);

        Assert.Equal(StatusCodes.Status200OK, replacementResponse.StatusCode);
        Assert.Equal(FleetOrderStatus.Superseded, pending.Status);
        Assert.Equal(replacement.FleetOrderId, pending.SupersededByOrderId);
        Assert.Equal(2, state.FleetOrders.Count);
    }

    [Fact]
    public async Task ColoniseOrderEndpointAcceptsOwnedFleetWithPopulationAndLeadingInfluence()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var fleet = Assert.Single(state.Fleets);
        fleet.CurrentSystemId = state.Systems.Single(system => system.SystemName == "Destination").SystemId;
        state.EmpireResources.Single().Population = OrderService.ColonisationPopulationCost;
        var game = new FocusedSelectedGameFixture(state);
        var httpContext = CreateAuthenticatedContext(state);

        var result = ApiOrderEndpoints.SubmitColonise(
            new ColoniseFleetRequest(fleet.FleetId),
            httpContext,
            game.GameId,
            game.Requests,
            TestState.Now);

        var response = await ExecuteAsync(result);
        var order = Assert.Single(state.FleetOrders);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(FleetOrderType.Colonise, order.OrderType);
        Assert.Equal(fleet.CurrentSystemId, order.TargetSystemId);
    }

    [Fact]
    public async Task ColoniseOrderEndpointRejectsFleetOwnedByAnotherEmpire()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 30, defenderShips: 20);
        foreach (var empire in state.Empires)
        {
            empire.HomeSystemId = Guid.NewGuid();
        }
        var firstPlayer = state.Players.Single(player => player.Username == "first");
        var secondFleet = state.Fleets.Single(fleet => fleet.EmpireId == state.Empires.Single(empire => empire.EmpireName == "Second").EmpireId);
        state.EmpireResources.Single(resource => resource.EmpireId == secondFleet.EmpireId).Population = OrderService.ColonisationPopulationCost;
        var game = new FocusedSelectedGameFixture(state);
        var httpContext = CreateAuthenticatedContext(state, firstPlayer);

        var result = ApiOrderEndpoints.SubmitColonise(
            new ColoniseFleetRequest(secondFleet.FleetId),
            httpContext,
            game.GameId,
            game.Requests,
            TestState.Now);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Contains("\"code\":\"forbidden\"", response.Body, StringComparison.Ordinal);
        Assert.Empty(state.FleetOrders);
    }

    [Fact]
    public async Task CancelOrderEndpointCancelsPendingOrderForOwningEmpire()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        var order = OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);
        var game = new FocusedSelectedGameFixture(state);
        var httpContext = CreateAuthenticatedContext(state);

        var result = ApiOrderEndpoints.Cancel(
            new CancelFleetOrderRequest(order.FleetOrderId),
            httpContext,
            game.GameId,
            game.Requests,
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
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        var order = OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);
        order.Status = FleetOrderStatus.Processed;
        order.ProcessedTick = 1;
        var game = new FocusedSelectedGameFixture(state);
        var httpContext = CreateAuthenticatedContext(state);

        var result = ApiOrderEndpoints.Cancel(
            new CancelFleetOrderRequest(order.FleetOrderId),
            httpContext,
            game.GameId,
            game.Requests,
            TestState.Now);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status409Conflict, response.StatusCode);
        Assert.Contains("Only pending orders", response.Body, StringComparison.Ordinal);
        Assert.Contains("\"code\":\"stateConflict\"", response.Body, StringComparison.Ordinal);
        Assert.Equal(FleetOrderStatus.Processed, order.Status);
    }

    [Fact]
    public async Task CancelOrderEndpointRejectsDefeatedParticipant()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var player = Assert.Single(state.Players);
        var empire = Assert.Single(state.Empires);
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        var order = OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);
        MatchControl.DefeatEmpire(state, empire.EmpireId, TestState.Now.AddMinutes(1));
        var game = new FocusedSelectedGameFixture(state);

        var result = ApiOrderEndpoints.Cancel(
            new CancelFleetOrderRequest(order.FleetOrderId),
            CreateAuthenticatedContext(state, player),
            game.GameId,
            game.Requests,
            TestState.Now.AddMinutes(2));
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(FleetOrderStatus.Pending, order.Status);
    }

    [Fact]
    public async Task PriorityEndpointRejectsWeightsThatDoNotTotalOneHundred()
    {
        var state = TestState.CreateSingleEmpireState();
        var game = new FocusedSelectedGameFixture(state);
        var httpContext = CreateAuthenticatedContext(state);

        var result = ApiOrderEndpoints.UpdatePriorities(
            new PriorityRequest(null, 25, 25, 25, 20),
            httpContext,
            game.GameId,
            game.Requests,
            TestState.Now);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("must total 100", response.Body, StringComparison.Ordinal);
        Assert.Empty(state.Events);
    }

    [Fact]
    public async Task PriorityEndpointRejectsInactiveWeights()
    {
        var state = TestState.CreateSingleEmpireState();
        var game = new FocusedSelectedGameFixture(state);
        var httpContext = CreateAuthenticatedContext(state);

        var result = ApiOrderEndpoints.UpdatePriorities(
            new PriorityRequest(null, 1, 0, 66, 33),
            httpContext,
            game.GameId,
            game.Requests,
            TestState.Now);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("locked at zero", response.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(state.Events);
    }

    [Fact]
    public async Task MoveOrderEndpointRequiresDevelopmentLogin()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        var game = new FocusedSelectedGameFixture(state);

        var result = ApiOrderEndpoints.SubmitMove(
            new MoveFleetRequest(fleet.FleetId, destination.SystemId),
            new DefaultHttpContext(),
            game.GameId,
            game.Requests,
            TestState.Now);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Contains("\"code\":\"authenticationRequired\"", response.Body, StringComparison.Ordinal);
        Assert.Empty(state.FleetOrders);
    }

    [Fact]
    public async Task AttackOrderEndpointRejectsFleetOwnedByAnotherEmpire()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 20, defenderShips: 20);
        var firstPlayer = state.Players.Single(player => player.Username == "first");
        var secondFleet = state.Fleets.Single(fleet => fleet.EmpireId == state.Empires.Single(empire => empire.EmpireName == "Second").EmpireId);
        var firstEmpire = state.Empires.Single(empire => empire.EmpireName == "First");
        var game = new FocusedSelectedGameFixture(state);
        var httpContext = CreateAuthenticatedContext(state, firstPlayer);

        var result = ApiOrderEndpoints.SubmitAttack(
            new AttackFleetRequest(secondFleet.FleetId, firstEmpire.EmpireId),
            httpContext,
            game.GameId,
            game.Requests,
            TestState.Now);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Empty(state.FleetOrders);
    }

    [Fact]
    public async Task AdminCanSubmitOrderForAnotherEmpireFleet()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 20, defenderShips: 20);
        var admin = state.Players.Single(player => player.Username == "first");
        admin.Role = PlayerRole.Admin;
        var secondFleet = state.Fleets.Single(fleet => fleet.EmpireId == state.Empires.Single(empire => empire.EmpireName == "Second").EmpireId);
        var firstEmpire = state.Empires.Single(empire => empire.EmpireName == "First");
        var game = new FocusedSelectedGameFixture(state);
        var httpContext = CreateAuthenticatedContext(state, admin);

        var result = ApiOrderEndpoints.SubmitAttack(
            new AttackFleetRequest(secondFleet.FleetId, firstEmpire.EmpireId),
            httpContext,
            game.GameId,
            game.Requests,
            TestState.Now);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Single(state.FleetOrders, order => order.FleetId == secondFleet.FleetId);
    }

    [Fact]
    public async Task PriorityEndpointRejectsAnotherEmpireForPlayer()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 20, defenderShips: 20);
        var firstPlayer = state.Players.Single(player => player.Username == "first");
        var secondEmpire = state.Empires.Single(empire => empire.EmpireName == "Second");
        var game = new FocusedSelectedGameFixture(state);
        var httpContext = CreateAuthenticatedContext(state, firstPlayer);

        var result = ApiOrderEndpoints.UpdatePriorities(
            new PriorityRequest(secondEmpire.EmpireId, 25, 25, 25, 25),
            httpContext,
            game.GameId,
            game.Requests,
            TestState.Now);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
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

    private static DefaultHttpContext CreateAuthenticatedContext(GameState state, Player? player = null)
    {
        var authenticatedPlayer = player ?? Assert.Single(state.Players);
        return TestHttpContextFactory.CreateAuthenticated(authenticatedPlayer);
    }

    private static IServiceProvider CreateResultServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureHttpJsonOptions(options =>
        {
            ApiJson.Configure(options.SerializerOptions);
        });

        return services.BuildServiceProvider();
    }

    private sealed class FocusedSelectedGameFixture :
        IPlayerAccountQuery,
        IGameAccessQuery,
        IGameCommandAccessQuery,
        ICycleViewQuery,
        ICycleCommandStore
    {
        private static readonly Guid SyntheticGameId = new("b358ff83-c7fd-4d44-90a4-bab7fa85ca72");
        private readonly GameState state;
        private readonly Cycle cycle;

        public FocusedSelectedGameFixture(GameState state)
        {
            this.state = state;
            cycle = state.GetActiveCycle()
                ?? throw new InvalidOperationException("The focused API test state requires one active Cycle.");
            GameId = cycle.GameId.GetValueOrDefault() is { } gameId && gameId != Guid.Empty
                ? gameId
                : SyntheticGameId;
            Requests = new SelectedGameRequestService(this, this, this, this, this);
        }

        public Guid GameId { get; }

        public SelectedGameRequestService Requests { get; }

        public PlayerAccountSnapshot? Get(Guid playerId)
        {
            var player = state.Players.SingleOrDefault(item => item.PlayerId == playerId);
            return player is null
                ? null
                : new PlayerAccountSnapshot(
                    player.PlayerId,
                    player.Username,
                    player.Kind,
                    player.Role,
                    player.Status,
                    player.CreatedAt,
                    player.LastLoginAt);
        }

        public GameAccessSnapshot? Get(Guid playerId, Guid gameId)
        {
            if (gameId != GameId || state.Players.All(item => item.PlayerId != playerId))
            {
                return null;
            }

            var participant = state.MatchParticipants.SingleOrDefault(item =>
                item.CycleId == cycle.CycleId && item.PlayerId == playerId);
            return new GameAccessSnapshot(
                playerId,
                GameId,
                "Focused API test Game",
                GamePurpose.Standard,
                GameLifecycleStatus.Active,
                GameVisibility.Private,
                cycle.CreatedByPlayerId,
                participant?.MatchParticipantId,
                participant is null ? null : GameEnrolmentStatus.Enrolled,
                cycle.CycleId,
                cycle.Status,
                cycle.CurrentTickNumber,
                cycle.TurnStage);
        }

        public GameCommandContext? Get(Guid playerId, GameCycleScope scope)
        {
            if (scope.GameId != GameId || scope.CycleId != cycle.CycleId)
            {
                return null;
            }

            var player = state.Players.SingleOrDefault(item => item.PlayerId == playerId);
            var participant = state.MatchParticipants.SingleOrDefault(item =>
                item.CycleId == cycle.CycleId && item.PlayerId == playerId);
            var empire = participant is null
                ? null
                : state.Empires.SingleOrDefault(item =>
                    item.CycleId == cycle.CycleId && item.EmpireId == participant.EmpireId);
            if (player is null || participant is null || empire is null)
            {
                return null;
            }

            var permissions = GamePermission.Read;
            if (cycle.CreatedByPlayerId == playerId)
            {
                permissions |= GamePermission.Organise;
            }

            if (player.Role == PlayerRole.Admin)
            {
                permissions |= GamePermission.Administer;
            }

            return new GameCommandContext(
                new GameAccessContext(
                    playerId,
                    GameId,
                    participant.MatchParticipantId,
                    permissions),
                cycle.CycleId,
                participant.MatchParticipantId,
                empire.EmpireId);
        }

        public ScopedQueryResult<T> Query<T>(
            GameCommandContext context,
            Func<GameState, T> projection) =>
            ContextMatches(context)
                ? new ScopedQueryResult<T>.Success(projection(state))
                : new ScopedQueryResult<T>.Unavailable();

        public ScopedCommandResult<T> Execute<T>(
            GameCommandContext context,
            Func<GameState, T> command)
        {
            if (!ContextMatches(context)
                || cycle.Status != CycleStatus.Active
                || state.MatchParticipants.Single(item =>
                    item.MatchParticipantId == context.MatchParticipantId).Status != MatchParticipantStatus.Active
                || state.Empires.Single(item => item.EmpireId == context.EmpireId).Status != EmpireStatus.Active)
            {
                return new ScopedCommandResult<T>.Unavailable();
            }

            return new ScopedCommandResult<T>.Success(command(state));
        }

        private bool ContextMatches(GameCommandContext context) =>
            context.GameAccess.GameId == GameId
            && context.CycleId == cycle.CycleId
            && state.Players.Any(item => item.PlayerId == context.GameAccess.PlayerId)
            && state.MatchParticipants.Any(item =>
                item.MatchParticipantId == context.MatchParticipantId
                && item.CycleId == context.CycleId
                && item.PlayerId == context.GameAccess.PlayerId
                && item.EmpireId == context.EmpireId)
            && state.Empires.Any(item =>
                item.EmpireId == context.EmpireId
                && item.CycleId == context.CycleId
                && item.PlayerId == context.GameAccess.PlayerId);
    }
}
