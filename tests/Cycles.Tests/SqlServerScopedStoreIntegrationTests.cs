using Cycles.Application;
using Cycles.Core;
using Cycles.Infrastructure.SqlServer;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace Cycles.Tests;

[Collection(SqlServerIntegrationCollection.CollectionName)]
[Trait(SqlIntegrationGuard.CategoryName, SqlIntegrationGuard.CategoryValue)]
public sealed class SqlServerScopedStoreIntegrationTests
{
    private static readonly DateTimeOffset CommandTime = new(2026, 7, 20, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Player_query_returns_a_redacted_account_projection()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        var query = (IPlayerAccountQuery)CreateStore(fixture.ConnectionString);

        var player = query.Get(fixture.Ids.PlayerA);

        Assert.NotNull(player);
        Assert.Equal(fixture.Ids.PlayerA, player.PlayerId);
        Assert.Equal(PlayerKind.Human, player.Kind);
        Assert.Equal(PlayerRole.Player, player.Role);
        Assert.Equal(PlayerStatus.Active, player.Status);
        var json = JsonSerializer.Serialize(player);
        Assert.DoesNotContain(fixture.SecretEmail, json, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.SecretPasswordHash, json, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.SecretIssuer, json, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.SecretSubject, json, StringComparison.Ordinal);
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("external", json, StringComparison.OrdinalIgnoreCase);
        Assert.Null(query.Get(Guid.NewGuid()));
    }

    [Fact]
    public void Game_catalogue_is_bounded_stably_paged_and_scoped_to_the_player()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        var query = (IGameCatalogueQuery)CreateStore(fixture.ConnectionString);

        var firstPage = query.ListForPlayer(fixture.Ids.PlayerA, cursor: null, pageSize: 1);
        var firstItem = Assert.Single(firstPage.Items);
        Assert.NotNull(firstPage.NextCursor);
        Assert.Equal(firstItem.EnrolmentStatusChangedAt, firstPage.NextCursor.SortAt);
        Assert.Equal(firstItem.GameId, firstPage.NextCursor.GameId);

        var secondPage = query.ListForPlayer(fixture.Ids.PlayerA, firstPage.NextCursor, pageSize: 1);
        var secondItem = Assert.Single(secondPage.Items);
        Assert.NotEqual(firstItem.GameId, secondItem.GameId);
        Assert.Null(secondPage.NextCursor);
        Assert.True(
            new HashSet<Guid> { fixture.Ids.GameA, fixture.Ids.GameB }
                .SetEquals([firstItem.GameId, secondItem.GameId]));
        Assert.DoesNotContain(fixture.HiddenGameId, new[] { firstItem.GameId, secondItem.GameId });

        var emptyPage = query.ListForPlayer(fixture.Ids.PlayerC, cursor: null, pageSize: 1);
        Assert.Empty(emptyPage.Items);
        Assert.Null(emptyPage.NextCursor);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            query.ListForPlayer(fixture.Ids.PlayerA, cursor: null, pageSize: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            query.ListForPlayer(
                fixture.Ids.PlayerA,
                cursor: null,
                pageSize: GameCataloguePage.MaximumPageSize + 1));
    }

    [Fact]
    public void Game_access_returns_neutral_unavailable_results_for_hidden_missing_and_disabled_player_requests()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        var query = (IGameAccessQuery)CreateStore(fixture.ConnectionString);

        var enrolled = query.Get(fixture.Ids.PlayerA, fixture.Ids.GameA);
        Assert.NotNull(enrolled);
        Assert.Equal(fixture.Ids.EnrolmentAA, enrolled.GameEnrolmentId);
        Assert.Null(query.Get(fixture.Ids.PlayerC, fixture.HiddenGameId));
        Assert.Null(query.Get(fixture.Ids.PlayerC, Guid.NewGuid()));

        Execute(
            fixture.ConnectionString,
            "UPDATE dbo.Players SET Status = N'Suspended' WHERE PlayerID = @PlayerID;",
            ("@PlayerID", fixture.Ids.PlayerA));

        Assert.Null(query.Get(fixture.Ids.PlayerA, fixture.Ids.GameA));
    }

    [Fact]
    public void Cycle_command_returns_unavailable_for_a_game_cycle_mismatch_without_invoking_the_callback()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        var store = (ICycleCommandStore)CreateStore(fixture.ConnectionString);
        var callbackInvoked = false;

        var result = store.Execute(
            new GameCycleScope(fixture.Ids.GameA, fixture.Ids.CycleB),
            _ =>
            {
                callbackInvoked = true;
                return 42;
            });

        Assert.IsType<ScopedCommandResult<int>.Unavailable>(result);
        Assert.False(callbackInvoked);
    }

    [Fact]
    public void Cycle_command_persists_a_target_order_and_event_without_touching_the_other_cycle()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        var beforeOtherCycle = ReadCycleFingerprint(fixture.ConnectionString, fixture.Ids.CycleB);
        var store = (ICycleCommandStore)CreateStore(fixture.ConnectionString);

        var result = store.Execute(
            new GameCycleScope(fixture.Ids.GameA, fixture.Ids.CycleA),
            state =>
            {
                Assert.Single(state.Cycles, cycle => cycle.CycleId == fixture.Ids.CycleA);
                Assert.DoesNotContain(state.Cycles, cycle => cycle.CycleId == fixture.Ids.CycleB);
                Assert.DoesNotContain(state.Fleets, fleet => fleet.FleetId == fixture.Ids.FleetB2);
                var order = OrderService.SubmitHoldOrder(state, fixture.Ids.FleetA2, CommandTime);
                var gameEvent = CreateEvent(fixture.Ids.CycleA, fixture.Ids.EmpireA2, fixture.Ids.SystemA2);
                state.Events.Add(gameEvent);
                return new CommandArtifacts(order.FleetOrderId, gameEvent.EventId);
            });

        var success = Assert.IsType<ScopedCommandResult<CommandArtifacts>.Success>(result);
        Assert.Equal(1, Scalar<int>(
            fixture.ConnectionString,
            "SELECT COUNT(*) FROM dbo.FleetOrders WHERE FleetOrderID = @ID AND CycleID = @CycleID;",
            ("@ID", success.Value.OrderId),
            ("@CycleID", fixture.Ids.CycleA)));
        Assert.Equal(1, Scalar<int>(
            fixture.ConnectionString,
            "SELECT COUNT(*) FROM dbo.Events WHERE EventID = @ID AND CycleID = @CycleID;",
            ("@ID", success.Value.EventId),
            ("@CycleID", fixture.Ids.CycleA)));
        Assert.Equal(beforeOtherCycle, ReadCycleFingerprint(fixture.ConnectionString, fixture.Ids.CycleB));
    }

    [Fact]
    public void Cycle_command_rejects_a_hostile_foreign_fleet_identifier_and_rolls_back()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        var beforeTarget = ReadCycleFingerprint(fixture.ConnectionString, fixture.Ids.CycleA);
        var beforeForeign = ReadCycleFingerprint(fixture.ConnectionString, fixture.Ids.CycleB);
        var store = (ICycleCommandStore)CreateStore(fixture.ConnectionString);
        var foreignFleetWasLoaded = false;

        Assert.Throws<InvalidOperationException>(() => store.Execute<FleetOrder>(
            new GameCycleScope(fixture.Ids.GameA, fixture.Ids.CycleA),
            state =>
            {
                foreignFleetWasLoaded = state.Fleets.Any(fleet => fleet.FleetId == fixture.Ids.FleetB2);
                state.Events.Add(CreateEvent(fixture.Ids.CycleA, fixture.Ids.EmpireA1, fixture.Ids.SystemA1));
                return OrderService.SubmitHoldOrder(state, fixture.Ids.FleetB2, CommandTime);
            }));

        Assert.False(foreignFleetWasLoaded);
        Assert.Equal(beforeTarget, ReadCycleFingerprint(fixture.ConnectionString, fixture.Ids.CycleA));
        Assert.Equal(beforeForeign, ReadCycleFingerprint(fixture.ConnectionString, fixture.Ids.CycleB));
    }

    [Fact]
    public void Cycle_command_rejects_foreign_cycle_injection_and_allowed_row_deletion()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        var beforeTarget = ReadCycleFingerprint(fixture.ConnectionString, fixture.Ids.CycleA);
        var beforeForeign = ReadCycleFingerprint(fixture.ConnectionString, fixture.Ids.CycleB);
        var store = (ICycleCommandStore)CreateStore(fixture.ConnectionString);
        var scope = new GameCycleScope(fixture.Ids.GameA, fixture.Ids.CycleA);

        Assert.Throws<InvalidOperationException>(() => store.Execute(
            scope,
            state =>
            {
                state.Events.Add(CreateEvent(
                    fixture.Ids.CycleB,
                    fixture.Ids.EmpireB1,
                    fixture.Ids.SystemB1));
                return state.Events.Count;
            }));
        Assert.Equal(beforeTarget, ReadCycleFingerprint(fixture.ConnectionString, fixture.Ids.CycleA));
        Assert.Equal(beforeForeign, ReadCycleFingerprint(fixture.ConnectionString, fixture.Ids.CycleB));

        Assert.Throws<InvalidOperationException>(() => store.Execute(
            scope,
            state =>
            {
                var collidingEvent = CreateEvent(
                    fixture.Ids.CycleA,
                    fixture.Ids.EmpireA1,
                    fixture.Ids.SystemA1);
                collidingEvent.EventId = fixture.Ids.EventB;
                state.Events.Add(collidingEvent);
                return collidingEvent.EventId;
            }));
        Assert.Equal(beforeTarget, ReadCycleFingerprint(fixture.ConnectionString, fixture.Ids.CycleA));
        Assert.Equal(beforeForeign, ReadCycleFingerprint(fixture.ConnectionString, fixture.Ids.CycleB));

        Assert.Throws<InvalidOperationException>(() => store.Execute(
            scope,
            state =>
            {
                var existing = state.FleetOrders.Single(order => order.FleetOrderId == fixture.Ids.OrderA2);
                state.FleetOrders.Remove(existing);
                return existing.FleetOrderId;
            }));
        Assert.Equal(beforeTarget, ReadCycleFingerprint(fixture.ConnectionString, fixture.Ids.CycleA));
        Assert.Equal(beforeForeign, ReadCycleFingerprint(fixture.ConnectionString, fixture.Ids.CycleB));
    }

    [Fact]
    public void Cycle_command_rolls_back_allowed_changes_when_the_callback_throws()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        var beforeTarget = ReadCycleFingerprint(fixture.ConnectionString, fixture.Ids.CycleA);
        var store = (ICycleCommandStore)CreateStore(fixture.ConnectionString);

        Assert.Throws<CallbackFailureException>(() => store.Execute<int>(
            new GameCycleScope(fixture.Ids.GameA, fixture.Ids.CycleA),
            state =>
            {
                state.FleetOrders.Add(CreateHoldOrder(fixture.Ids.CycleA, fixture.Ids.FleetA2));
                state.Events.Add(CreateEvent(fixture.Ids.CycleA, fixture.Ids.EmpireA2, fixture.Ids.SystemA2));
                throw new CallbackFailureException();
            }));

        Assert.Equal(beforeTarget, ReadCycleFingerprint(fixture.ConnectionString, fixture.Ids.CycleA));
    }

    [Fact]
    public void Cycle_command_rejects_a_disallowed_fleet_mutation_and_rolls_back_every_change()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        var beforeTarget = ReadCycleFingerprint(fixture.ConnectionString, fixture.Ids.CycleA);
        var store = (ICycleCommandStore)CreateStore(fixture.ConnectionString);

        Assert.Throws<InvalidOperationException>(() => store.Execute(
            new GameCycleScope(fixture.Ids.GameA, fixture.Ids.CycleA),
            state =>
            {
                var fleet = state.Fleets.Single(item => item.FleetId == fixture.Ids.FleetA2);
                fleet.ShipCount++;
                state.Events.Add(CreateEvent(fixture.Ids.CycleA, fixture.Ids.EmpireA2, fixture.Ids.SystemA2));
                return fleet.ShipCount;
            }));

        Assert.Equal(beforeTarget, ReadCycleFingerprint(fixture.ConnectionString, fixture.Ids.CycleA));
    }

    private static SqlServerGameStateStore CreateStore(string connectionString) =>
        new(connectionString, () => new GameState());

    private static ScopedStoreFixture? CreateFixture()
    {
        var connectionString = Environment.GetEnvironmentVariable(SqlIntegrationGuard.ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var hiddenGameId = Guid.NewGuid();
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var ids = SqlServerCycleScopeFixture.Insert(connection, transaction);
        var secretEmail = $"private-{ids.PlayerA:D}@example.test";
        var secretPasswordHash = $"private-password-hash-{ids.PlayerA:D}";
        var secretIssuer = $"https://private-{ids.PlayerA:D}.identity.example";
        var secretSubject = $"private-subject-{ids.PlayerA:D}";
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE dbo.Players
            SET Email = @Email,
                PasswordHash = @PasswordHash,
                ExternalIssuer = @ExternalIssuer,
                ExternalSubject = @ExternalSubject
            WHERE PlayerID = @PlayerID;

            UPDATE dbo.FleetOrders
            SET OrderType = N'MoveFleet',
                Status = N'Pending',
                ProcessedTick = NULL
            WHERE FleetOrderID = @LoadedOrderID;

            INSERT INTO dbo.Games
                (GameID, Name, Purpose, Status, Visibility, CreationSource, GamePolicyKey, GamePolicyVersion,
                 GamePolicyContentHash, PolicyProvenanceStatus, CreatedByPlayerID, CreatedAt, FirstStartedAt,
                 CompletedAt, CancelledAt, TerminatedAt)
            VALUES
                (@HiddenGameID, N'Hidden scope Game', N'Standard', N'Forming', N'Private', N'Operator',
                 N'scope-hidden-policy', 1, NULL, N'Verified', NULL, '2026-07-20T12:00:00+00:00',
                 NULL, NULL, NULL, NULL);
            """;
        command.Parameters.AddWithValue("@Email", secretEmail);
        command.Parameters.AddWithValue("@PasswordHash", secretPasswordHash);
        command.Parameters.AddWithValue("@ExternalIssuer", secretIssuer);
        command.Parameters.AddWithValue("@ExternalSubject", secretSubject);
        command.Parameters.AddWithValue("@PlayerID", ids.PlayerA);
        command.Parameters.AddWithValue("@LoadedOrderID", ids.OrderA2);
        command.Parameters.AddWithValue("@HiddenGameID", hiddenGameId);
        command.ExecuteNonQuery();
        transaction.Commit();
        return new ScopedStoreFixture(
            connectionString,
            ids,
            hiddenGameId,
            secretEmail,
            secretPasswordHash,
            secretIssuer,
            secretSubject);
    }

    private static FleetOrder CreateHoldOrder(Guid cycleId, Guid fleetId) => new()
    {
        CycleId = cycleId,
        FleetId = fleetId,
        OrderType = FleetOrderType.Hold,
        SubmitTick = 1,
        ExecuteAfterTick = 2,
        Status = FleetOrderStatus.Pending,
        CommandSource = FleetOrderCommandSource.Human,
        CreatedAt = CommandTime
    };

    private static EventRecord CreateEvent(Guid cycleId, Guid empireId, Guid systemId) => new()
    {
        CycleId = cycleId,
        TickNumber = 1,
        EventType = EventType.PrioritiesChanged,
        SystemId = systemId,
        EmpireId = empireId,
        FactionId = empireId,
        Severity = EventSeverity.Low,
        FactJson = "{\"source\":\"scoped-store-test\"}",
        DisplayText = "Scoped command persisted an event.",
        CreatedAt = CommandTime
    };

    private static string ReadCycleFingerprint(string connectionString, Guid cycleId)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                JSON_QUERY((
                    SELECT CycleID, GameID, CurrentTickNumber, Status, TurnStage
                    FROM dbo.Cycles
                    WHERE CycleID = @CycleID
                    FOR JSON PATH, INCLUDE_NULL_VALUES
                )) AS [Cycle],
                JSON_QUERY((
                    SELECT FleetOrderID, CycleID, FleetID, OrderType, TargetSystemID, TargetEmpireID, TargetFactionID,
                           SubmitTick, ExecuteAfterTick, ProcessedTick, Status, CommandSource, SealedTick, SealedAt,
                           RejectionReason, SupersededByOrderID, CreatedAt
                    FROM dbo.FleetOrders
                    WHERE CycleID = @CycleID
                    ORDER BY FleetOrderID
                    FOR JSON PATH, INCLUDE_NULL_VALUES
                )) AS [Orders],
                JSON_QUERY((
                    SELECT EventID, CycleID, TickNumber, EventType, SystemID, EmpireID, FactionID, Severity,
                           FactJson, DisplayText, CreatedAt
                    FROM dbo.Events
                    WHERE CycleID = @CycleID
                    ORDER BY EventID
                    FOR JSON PATH, INCLUDE_NULL_VALUES
                )) AS [Events],
                JSON_QUERY((
                    SELECT FleetID, CycleID, EmpireID, FactionID, AdmiralID, FleetName, CurrentSystemID,
                           DestinationSystemID, DepartureTickNumber, ArrivalTickNumber, ShipCount, Status, CreatedAt
                    FROM dbo.Fleets
                    WHERE CycleID = @CycleID
                    ORDER BY FleetID
                    FOR JSON PATH, INCLUDE_NULL_VALUES
                )) AS [Fleets]
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;
            """;
        command.Parameters.AddWithValue("@CycleID", cycleId);
        return (string)command.ExecuteScalar()!;
    }

    private static T Scalar<T>(
        string connectionString,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameters(command, parameters);
        return (T)Convert.ChangeType(command.ExecuteScalar()!, typeof(T));
    }

    private static void Execute(
        string connectionString,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameters(command, parameters);
        command.ExecuteNonQuery();
    }

    private static void AddParameters(
        SqlCommand command,
        IEnumerable<(string Name, object Value)> parameters)
    {
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
    }

    private sealed record CommandArtifacts(Guid OrderId, Guid EventId);

    private sealed record ScopedStoreFixture(
        string ConnectionString,
        SqlServerCycleScopeFixtureIds Ids,
        Guid HiddenGameId,
        string SecretEmail,
        string SecretPasswordHash,
        string SecretIssuer,
        string SecretSubject);

    private sealed class CallbackFailureException : Exception
    {
    }
}
