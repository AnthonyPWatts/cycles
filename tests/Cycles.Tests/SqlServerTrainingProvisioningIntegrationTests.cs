using Cycles.Application;
using Cycles.Core;
using Cycles.Infrastructure.SqlServer;

namespace Cycles.Tests;

[Collection(SqlServerIntegrationCollection.CollectionName)]
[Trait(SqlIntegrationGuard.CategoryName, SqlIntegrationGuard.CategoryValue)]
public sealed class SqlServerTrainingProvisioningIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Concurrent_requests_create_one_private_training_game_and_return_the_same_attempt()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var player = CreatePlayer();
        var store = PrepareStore(connectionString, player);
        using var start = new Barrier(3);
        var tasks = Enumerable.Range(0, 2)
            .Select(_ => Task.Run(() =>
            {
                Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(5)));
                return store.ProvisionTwinReaches(
                    new TrainingGameProvisioningCommand(player.PlayerId, Guid.NewGuid(), Now));
            }))
            .ToArray();

        Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(5)));
        var results = (await Task.WhenAll(tasks))
            .Select(result => Assert.IsType<TrainingGameProvisioningResult.Success>(result).Value)
            .ToArray();

        Assert.Single(results, result => result.Created);
        Assert.Single(results.Select(result => result.GameId).Distinct());
        Assert.Single(results.Select(result => result.CycleId).Distinct());

        var persisted = store.LoadOrCreate();
        var game = Assert.Single(persisted.Games);
        var cycle = Assert.Single(persisted.Cycles);
        Assert.Equal(results[0].GameId, game.GameId);
        Assert.Equal(results[0].CycleId, cycle.CycleId);
        Assert.Equal(GamePurpose.Training, game.Purpose);
        Assert.Equal(GameVisibility.Private, game.Visibility);
        Assert.Equal(CycleSchedulingMode.SelfPaced, cycle.SchedulingMode);
        Assert.Null(cycle.NextTickAt);
        Assert.Single(persisted.GameEnrolments);
        Assert.Single(persisted.MatchParticipants);
        Assert.Equal(2, persisted.Sectors.Count);
        Assert.Equal(10, persisted.Systems.Count);
        Assert.Equal(13, persisted.SystemLinks.Count);
    }

    [Fact]
    public void First_normal_move_resolves_and_is_visible_after_a_new_store_returns_to_the_game()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var player = CreatePlayer();
        var store = PrepareStore(connectionString, player);
        var provisioned = Assert.IsType<TrainingGameProvisioningResult.Success>(
            store.ProvisionTwinReaches(
                new TrainingGameProvisioningCommand(player.PlayerId, Guid.NewGuid(), Now))).Value;
        var scope = new GameCycleScope(provisioned.GameId, provisioned.CycleId);
        var context = ((IGameCommandAccessQuery)store).Get(player.PlayerId, scope);
        Assert.NotNull(context);

        var move = Assert.IsType<ScopedQueryResult<MoveFixture>.Success>(
            ((ICycleViewQuery)store).Query(context!, state =>
            {
                var fleet = state.Fleets.Single(item => item.FleetName == "Home Guard");
                var destination = state.Systems.Single(item => item.SystemName == "Firstlight");
                return new MoveFixture(fleet.FleetId, destination.SystemId);
            })).Value;
        var submitted = ((ICycleCommandStore)store).Execute(
            context!,
            state => OrderService.SubmitMoveOrder(
                state,
                move.FleetId,
                move.DestinationSystemId,
                Now.AddMinutes(1)));
        Assert.IsType<ScopedCommandResult<FleetOrder>.Success>(submitted);

        var resolved = store.ResolveExplicit(
            new ExplicitCycleResolutionRequest(context!, requireAdminister: false),
            Now.AddMinutes(2));
        Assert.IsType<CycleResolutionResult.Completed>(resolved);

        var returnedStore = new SqlServerGameStateStore(connectionString, () => new GameState());
        var returnedContext = ((IGameCommandAccessQuery)returnedStore).Get(player.PlayerId, scope);
        Assert.NotNull(returnedContext);
        var returned = Assert.IsType<ScopedQueryResult<ReturnedMove>.Success>(
            ((ICycleViewQuery)returnedStore).Query(returnedContext!, state =>
            {
                var cycle = state.Cycles.Single(item => item.CycleId == provisioned.CycleId);
                var fleet = state.Fleets.Single(item => item.FleetId == move.FleetId);
                var order = state.FleetOrders.Single(item => item.FleetId == move.FleetId);
                return new ReturnedMove(
                    cycle.CurrentTickNumber,
                    fleet.CurrentSystemId,
                    order.Status);
            })).Value;

        Assert.Equal(1, returned.TickNumber);
        Assert.Equal(move.DestinationSystemId, returned.CurrentSystemId);
        Assert.Equal(FleetOrderStatus.Processed, returned.OrderStatus);
        Assert.False(
            ((IDueCycleQuery)returnedStore).GetNextDue(Now.AddDays(1))?.Scope.CycleId
                == provisioned.CycleId);
    }

    private static SqlServerGameStateStore PrepareStore(string connectionString, Player player)
    {
        var store = new SqlServerGameStateStore(connectionString, () => new GameState());
        store.Replace(new GameState { Players = [player] });
        return store;
    }

    private static Player CreatePlayer() => new()
    {
        PlayerId = Guid.NewGuid(),
        Username = "training-provisioning-player",
        Kind = PlayerKind.Human,
        Role = PlayerRole.Player,
        Status = PlayerStatus.Active,
        CreatedAt = Now.AddDays(-1)
    };

    private sealed record MoveFixture(Guid FleetId, Guid DestinationSystemId);

    private sealed record ReturnedMove(
        int TickNumber,
        Guid CurrentSystemId,
        FleetOrderStatus OrderStatus);
}
