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
        Assert.All(new[] { firstItem, secondItem }, item =>
        {
            Assert.NotNull(item.FirstStartedAt);
            Assert.Equal(60, item.TickLengthMinutes);
            Assert.NotNull(item.NextTickAt);
        });
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
    public void Command_context_query_resolves_only_the_exact_player_game_cycle_tuple()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        var query = (IGameCommandAccessQuery)CreateStore(fixture.ConnectionString);

        var context = Assert.IsType<GameCommandContext>(query.Get(
            fixture.Ids.PlayerA,
            new GameCycleScope(fixture.Ids.GameA, fixture.Ids.CycleA)));

        Assert.Equal(fixture.Ids.PlayerA, context.GameAccess.PlayerId);
        Assert.Equal(fixture.Ids.GameA, context.GameAccess.GameId);
        Assert.Equal(fixture.Ids.EnrolmentAA, context.GameAccess.GameEnrolmentId);
        Assert.Equal(fixture.Ids.CycleA, context.CycleId);
        Assert.Equal(fixture.Ids.ParticipantA1, context.MatchParticipantId);
        Assert.Equal(fixture.Ids.EmpireA1, context.EmpireId);
        Assert.Equal(GamePermission.Read | GamePermission.Organise, context.GameAccess.Permissions);
        Assert.NotEqual(fixture.Ids.GameB, context.GameAccess.GameId);
        Assert.NotEqual(fixture.Ids.EnrolmentBA, context.GameAccess.GameEnrolmentId);
        Assert.NotEqual(fixture.Ids.CycleB, context.CycleId);
        Assert.NotEqual(fixture.Ids.ParticipantB1, context.MatchParticipantId);
        Assert.NotEqual(fixture.Ids.EmpireB1, context.EmpireId);
        Assert.Null(query.Get(
            fixture.Ids.PlayerA,
            new GameCycleScope(fixture.Ids.GameA, fixture.Ids.CycleB)));
        Assert.Null(query.Get(
            fixture.Ids.PlayerA,
            new GameCycleScope(fixture.Ids.GameB, fixture.Ids.CycleA)));
    }

    [Fact]
    public void Cycle_view_loads_complete_target_data_without_other_cycle_rows_or_player_secrets()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        var store = CreateStore(fixture.ConnectionString);
        var context = ResolveContext(store, fixture);

        var result = ((ICycleViewQuery)store).Query(context, state => state);

        var state = Assert.IsType<ScopedQueryResult<GameState>.Success>(result).Value;
        Assert.Equal(fixture.Ids.GameA, Assert.Single(state.Games).GameId);
        Assert.Equal(fixture.Ids.ConfigurationA, Assert.Single(state.CycleConfigurations).CycleConfigurationId);
        Assert.Equal(fixture.Ids.EnrolmentAA, Assert.Single(state.GameEnrolments).GameEnrolmentId);
        Assert.Equal(fixture.Ids.CycleA, Assert.Single(state.Cycles).CycleId);
        Assert.True(new HashSet<Guid> { fixture.Ids.PlayerA, fixture.Ids.PlayerB, fixture.Ids.PlayerC }
            .SetEquals(state.Players.Select(player => player.PlayerId)));
        Assert.All(state.Players, AssertPlayerIsRedacted);

        var json = JsonSerializer.Serialize(state);
        foreach (var expectedId in TargetViewIdentifiers(fixture.Ids))
        {
            Assert.Contains(expectedId.ToString("D"), json, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var excludedId in ExcludedViewIdentifiers(fixture))
        {
            Assert.DoesNotContain(excludedId.ToString("D"), json, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(fixture.SecretEmail, json, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.SecretPasswordHash, json, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.SecretIssuer, json, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.SecretSubject, json, StringComparison.Ordinal);
    }

    [Fact]
    public void Defeated_participation_remains_readable_but_completed_operational_scope_and_withdrawal_do_not()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        var store = CreateStore(fixture.ConnectionString);
        var context = ResolveContext(store, fixture);

        Execute(
            fixture.ConnectionString,
            """
            UPDATE dbo.MatchParticipants
            SET Status = N'Defeated', EndedAt = @EndedAt
            WHERE MatchParticipantID = @ParticipantID;
            UPDATE dbo.Empires SET Status = N'Defeated' WHERE EmpireID = @EmpireID;
            """,
            ("@EndedAt", CommandTime),
            ("@ParticipantID", fixture.Ids.ParticipantA1),
            ("@EmpireID", fixture.Ids.EmpireA1));

        Assert.NotNull(((IGameCommandAccessQuery)store).Get(
            fixture.Ids.PlayerA,
            new GameCycleScope(fixture.Ids.GameA, fixture.Ids.CycleA)));
        AssertReadableButNotCommandable(store, context, "defeated participation");

        Execute(
            fixture.ConnectionString,
            """
            UPDATE dbo.Games SET Status = N'Completed', CompletedAt = @EndedAt WHERE GameID = @GameID;
            UPDATE dbo.Cycles SET Status = N'Completed', NextTickAt = NULL WHERE CycleID = @CycleID;
            UPDATE dbo.GameEnrolments
            SET Status = N'Completed', EndedAt = @EndedAt
            WHERE GameEnrolmentID = @EnrolmentID;
            UPDATE dbo.MatchParticipants
            SET Status = N'Completed', EndedAt = @EndedAt
            WHERE MatchParticipantID = @ParticipantID;
            """,
            ("@EndedAt", CommandTime),
            ("@GameID", fixture.Ids.GameA),
            ("@CycleID", fixture.Ids.CycleA),
            ("@EnrolmentID", fixture.Ids.EnrolmentAA),
            ("@ParticipantID", fixture.Ids.ParticipantA1));

        Assert.NotNull(((IGameCommandAccessQuery)store).Get(
            fixture.Ids.PlayerA,
            new GameCycleScope(fixture.Ids.GameA, fixture.Ids.CycleA)));
        AssertContextIsUnavailable(store, context, "completed Game and Cycle");

        Execute(
            fixture.ConnectionString,
            "UPDATE dbo.GameEnrolments SET Status = N'Withdrawn' WHERE GameEnrolmentID = @EnrolmentID;",
            ("@EnrolmentID", fixture.Ids.EnrolmentAA));

        Assert.Null(((IGameCommandAccessQuery)store).Get(
            fixture.Ids.PlayerA,
            new GameCycleScope(fixture.Ids.GameA, fixture.Ids.CycleA)));
        var viewCallbackInvoked = false;
        var viewResult = ((ICycleViewQuery)store).Query(
            context,
            _ =>
            {
                viewCallbackInvoked = true;
                return 42;
            });
        Assert.IsType<ScopedQueryResult<int>.Unavailable>(viewResult);
        Assert.False(viewCallbackInvoked);

        var commandCallbackInvoked = false;
        var commandResult = ((ICycleCommandStore)store).Execute(
            context,
            _ =>
            {
                commandCallbackInvoked = true;
                return 42;
            });
        Assert.IsType<ScopedCommandResult<int>.Unavailable>(commandResult);
        Assert.False(commandCallbackInvoked);
    }

    [Fact]
    public void Recovery_required_operational_cycle_remains_readable()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        var store = CreateStore(fixture.ConnectionString);
        var context = ResolveContext(store, fixture);
        Execute(
            fixture.ConnectionString,
            """
            UPDATE dbo.Cycles
            SET Status = N'RecoveryRequired',
                TurnStage = N'Resolving',
                NextTickAt = NULL
            WHERE CycleID = @CycleID;
            """,
            ("@CycleID", fixture.Ids.CycleA));

        var result = ((ICycleViewQuery)store).Query(context, state =>
            state.Cycles.Single(item => item.CycleId == fixture.Ids.CycleA).Status);

        var success = Assert.IsType<ScopedQueryResult<CycleStatus>.Success>(result);
        Assert.Equal(CycleStatus.RecoveryRequired, success.Value);
    }

    [Fact]
    public void Cycle_command_revalidates_every_live_authority_status_before_invoking_the_callback()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        var store = CreateStore(fixture.ConnectionString);
        var context = ResolveContext(store, fixture);
        var mutations = new[]
        {
            new CommandAuthorityMutation(
                "suspended Player",
                "UPDATE dbo.Players SET Status = N'Suspended' WHERE PlayerID = @ID;",
                "UPDATE dbo.Players SET Status = N'Active' WHERE PlayerID = @ID;",
                fixture.Ids.PlayerA),
            new CommandAuthorityMutation(
                "AI Player",
                "UPDATE dbo.Players SET PlayerKind = N'AI' WHERE PlayerID = @ID;",
                "UPDATE dbo.Players SET PlayerKind = N'Human' WHERE PlayerID = @ID;",
                fixture.Ids.PlayerA),
            new CommandAuthorityMutation(
                "historical enrolment",
                "UPDATE dbo.GameEnrolments SET Status = N'Historical' WHERE GameEnrolmentID = @ID;",
                "UPDATE dbo.GameEnrolments SET Status = N'Enrolled' WHERE GameEnrolmentID = @ID;",
                fixture.Ids.EnrolmentAA),
            new CommandAuthorityMutation(
                "completed enrolment",
                "UPDATE dbo.GameEnrolments SET Status = N'Completed', EndedAt = '2026-07-20T13:00:00+00:00' WHERE GameEnrolmentID = @ID;",
                "UPDATE dbo.GameEnrolments SET Status = N'Enrolled', EndedAt = NULL WHERE GameEnrolmentID = @ID;",
                fixture.Ids.EnrolmentAA),
            new CommandAuthorityMutation(
                "completed Game",
                "UPDATE dbo.Games SET Status = N'Completed', CompletedAt = '2026-07-20T13:00:00+00:00' WHERE GameID = @ID;",
                "UPDATE dbo.Games SET Status = N'Active', CompletedAt = NULL WHERE GameID = @ID;",
                fixture.Ids.GameA),
            new CommandAuthorityMutation(
                "completed Cycle",
                "UPDATE dbo.Cycles SET Status = N'Completed', NextTickAt = NULL WHERE CycleID = @ID;",
                "UPDATE dbo.Cycles SET Status = N'Active', NextTickAt = '2026-07-20T12:00:00+00:00' WHERE CycleID = @ID;",
                fixture.Ids.CycleA),
            new CommandAuthorityMutation(
                "defeated participant",
                "UPDATE dbo.MatchParticipants SET Status = N'Defeated' WHERE MatchParticipantID = @ID;",
                "UPDATE dbo.MatchParticipants SET Status = N'Active' WHERE MatchParticipantID = @ID;",
                fixture.Ids.ParticipantA1),
            new CommandAuthorityMutation(
                "ended participant",
                "UPDATE dbo.MatchParticipants SET EndedAt = '2026-07-20T13:00:00+00:00' WHERE MatchParticipantID = @ID;",
                "UPDATE dbo.MatchParticipants SET EndedAt = NULL WHERE MatchParticipantID = @ID;",
                fixture.Ids.ParticipantA1),
            new CommandAuthorityMutation(
                "defeated Empire",
                "UPDATE dbo.Empires SET Status = N'Defeated' WHERE EmpireID = @ID;",
                "UPDATE dbo.Empires SET Status = N'Active' WHERE EmpireID = @ID;",
                fixture.Ids.EmpireA1)
        };

        var baseline = ((ICycleCommandStore)store).Execute(context, _ => 7);
        Assert.Equal(7, Assert.IsType<ScopedCommandResult<int>.Success>(baseline).Value);

        foreach (var mutation in mutations)
        {
            Execute(fixture.ConnectionString, mutation.ApplySql, ("@ID", mutation.Id));
            try
            {
                var callbackInvoked = false;
                var result = ((ICycleCommandStore)store).Execute(
                    context,
                    _ =>
                    {
                        callbackInvoked = true;
                        return 42;
                    });

                Assert.True(
                    result is ScopedCommandResult<int>.Unavailable,
                    $"Expected neutral command unavailability for {mutation.Description}, but got {result.GetType().Name}.");
                Assert.False(callbackInvoked, $"The command callback ran for {mutation.Description}.");
            }
            finally
            {
                Execute(fixture.ConnectionString, mutation.RestoreSql, ("@ID", mutation.Id));
            }
        }
    }

    [Fact]
    public void Hostile_context_identifier_combinations_are_neutrally_unavailable()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        var store = CreateStore(fixture.ConnectionString);
        var contexts = new[]
        {
            CreateContext(fixture, playerId: fixture.Ids.PlayerB),
            CreateContext(fixture, gameId: fixture.Ids.GameB),
            CreateContext(fixture, gameEnrolmentId: fixture.Ids.EnrolmentAB),
            CreateContext(fixture, cycleId: fixture.Ids.CycleB),
            CreateContext(fixture, matchParticipantId: fixture.Ids.ParticipantB1),
            CreateContext(fixture, empireId: fixture.Ids.EmpireB1)
        };

        foreach (var context in contexts)
        {
            var viewCallbackInvoked = false;
            var viewResult = ((ICycleViewQuery)store).Query(
                context,
                _ =>
                {
                    viewCallbackInvoked = true;
                    return 42;
                });
            Assert.IsType<ScopedQueryResult<int>.Unavailable>(viewResult);
            Assert.False(viewCallbackInvoked);

            var commandCallbackInvoked = false;
            var commandResult = ((ICycleCommandStore)store).Execute(
                context,
                _ =>
                {
                    commandCallbackInvoked = true;
                    return 42;
                });
            Assert.IsType<ScopedCommandResult<int>.Unavailable>(commandResult);
            Assert.False(commandCallbackInvoked);
        }
    }

    [Fact]
    public void Stale_elevated_permissions_are_revalidated_before_view_or_command_callbacks()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        var store = CreateStore(fixture.ConnectionString);
        var organiserContext = ResolveContext(store, fixture);
        Assert.True(organiserContext.GameAccess.Permissions.HasFlag(GamePermission.Organise));

        Execute(
            fixture.ConnectionString,
            "UPDATE dbo.Games SET CreatedByPlayerID = @NewCreatorID WHERE GameID = @GameID;",
            ("@NewCreatorID", fixture.Ids.PlayerB),
            ("@GameID", fixture.Ids.GameA));

        AssertContextIsUnavailable(store, organiserContext, "revoked organiser permission");

        Execute(
            fixture.ConnectionString,
            """
            UPDATE dbo.Games SET CreatedByPlayerID = @PlayerID WHERE GameID = @GameID;
            UPDATE dbo.Players SET Role = N'Admin' WHERE PlayerID = @PlayerID;
            """,
            ("@PlayerID", fixture.Ids.PlayerA),
            ("@GameID", fixture.Ids.GameA));

        var administratorContext = ResolveContext(store, fixture);
        Assert.True(administratorContext.GameAccess.Permissions.HasFlag(GamePermission.Administer));

        Execute(
            fixture.ConnectionString,
            "UPDATE dbo.Players SET Role = N'Player' WHERE PlayerID = @PlayerID;",
            ("@PlayerID", fixture.Ids.PlayerA));

        AssertContextIsUnavailable(store, administratorContext, "revoked administrator permission");
    }

    [Fact]
    public void Cycle_command_returns_busy_when_a_second_connection_holds_the_cycle_lock()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        var store = CreateStore(fixture.ConnectionString);
        var context = ResolveContext(store, fixture);
        using var blockingConnection = new SqlConnection(fixture.ConnectionString);
        blockingConnection.Open();
        using var blockingTransaction = blockingConnection.BeginTransaction();
        using var lockCommand = blockingConnection.CreateCommand();
        lockCommand.Transaction = blockingTransaction;
        lockCommand.CommandText = """
            DECLARE @Result int;
            EXEC @Result = sys.sp_getapplock
                @Resource = @Resource,
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = 0;
            SELECT @Result;
            """;
        lockCommand.Parameters.AddWithValue("@Resource", $"Cycles.Tick.{fixture.Ids.CycleA:D}");
        var lockResult = Convert.ToInt32(lockCommand.ExecuteScalar(), null);
        Assert.True(lockResult >= 0, $"Could not acquire the blocking Cycle lock. Result code: {lockResult}.");

        var callbackInvoked = false;
        var result = ((ICycleCommandStore)store).Execute(
            context,
            _ =>
            {
                callbackInvoked = true;
                return 42;
            });

        Assert.IsType<ScopedCommandResult<int>.Busy>(result);
        Assert.False(callbackInvoked);
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
            CreateContext(fixture, cycleId: fixture.Ids.CycleB),
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
        var sqlStore = CreateStore(fixture.ConnectionString);
        var store = (ICycleCommandStore)sqlStore;
        var context = ResolveContext(sqlStore, fixture);

        var result = store.Execute(
            context,
            state =>
            {
                Assert.Single(state.Cycles, cycle => cycle.CycleId == fixture.Ids.CycleA);
                Assert.DoesNotContain(state.Cycles, cycle => cycle.CycleId == fixture.Ids.CycleB);
                Assert.DoesNotContain(state.Fleets, fleet => fleet.FleetId == fixture.Ids.FleetB2);
                Assert.All(state.Players, AssertPlayerIsRedacted);
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
        var sqlStore = CreateStore(fixture.ConnectionString);
        var store = (ICycleCommandStore)sqlStore;
        var context = ResolveContext(sqlStore, fixture);
        var foreignFleetWasLoaded = false;

        Assert.Throws<InvalidOperationException>(() => store.Execute<FleetOrder>(
            context,
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
        var sqlStore = CreateStore(fixture.ConnectionString);
        var store = (ICycleCommandStore)sqlStore;
        var context = ResolveContext(sqlStore, fixture);

        Assert.Throws<InvalidOperationException>(() => store.Execute(
            context,
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
            context,
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
            context,
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
        var sqlStore = CreateStore(fixture.ConnectionString);
        var store = (ICycleCommandStore)sqlStore;
        var context = ResolveContext(sqlStore, fixture);

        Assert.Throws<CallbackFailureException>(() => store.Execute<int>(
            context,
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
        var sqlStore = CreateStore(fixture.ConnectionString);
        var store = (ICycleCommandStore)sqlStore;
        var context = ResolveContext(sqlStore, fixture);

        Assert.Throws<InvalidOperationException>(() => store.Execute(
            context,
            state =>
            {
                var fleet = state.Fleets.Single(item => item.FleetId == fixture.Ids.FleetA2);
                fleet.ShipCount++;
                state.Events.Add(CreateEvent(fixture.Ids.CycleA, fixture.Ids.EmpireA2, fixture.Ids.SystemA2));
                return fleet.ShipCount;
            }));

        Assert.Equal(beforeTarget, ReadCycleFingerprint(fixture.ConnectionString, fixture.Ids.CycleA));
    }

    private static GameCommandContext ResolveContext(
        SqlServerGameStateStore store,
        ScopedStoreFixture fixture) =>
        Assert.IsType<GameCommandContext>(((IGameCommandAccessQuery)store).Get(
            fixture.Ids.PlayerA,
            new GameCycleScope(fixture.Ids.GameA, fixture.Ids.CycleA)));

    private static GameCommandContext CreateContext(
        ScopedStoreFixture fixture,
        Guid? playerId = null,
        Guid? gameId = null,
        Guid? gameEnrolmentId = null,
        Guid? cycleId = null,
        Guid? matchParticipantId = null,
        Guid? empireId = null) =>
        new(
            new GameAccessContext(
                playerId ?? fixture.Ids.PlayerA,
                gameId ?? fixture.Ids.GameA,
                gameEnrolmentId ?? fixture.Ids.EnrolmentAA,
                GamePermission.Read),
            cycleId ?? fixture.Ids.CycleA,
            matchParticipantId ?? fixture.Ids.ParticipantA1,
            empireId ?? fixture.Ids.EmpireA1);

    private static void AssertReadableButNotCommandable(
        SqlServerGameStateStore store,
        GameCommandContext context,
        string stateDescription)
    {
        var viewCallbackInvoked = false;
        var viewResult = ((ICycleViewQuery)store).Query(
            context,
            state =>
            {
                viewCallbackInvoked = true;
                return state.Cycles.Count;
            });
        Assert.True(
            viewResult is ScopedQueryResult<int>.Success,
            $"Expected {stateDescription} to remain readable, but got {viewResult.GetType().Name}.");
        Assert.True(viewCallbackInvoked, $"The view callback did not run for {stateDescription}.");

        var commandCallbackInvoked = false;
        var commandResult = ((ICycleCommandStore)store).Execute(
            context,
            _ =>
            {
                commandCallbackInvoked = true;
                return 42;
            });
        Assert.True(
            commandResult is ScopedCommandResult<int>.Unavailable,
            $"Expected commands to be unavailable for {stateDescription}, but got {commandResult.GetType().Name}.");
        Assert.False(commandCallbackInvoked, $"The command callback ran for {stateDescription}.");
    }

    private static void AssertContextIsUnavailable(
        SqlServerGameStateStore store,
        GameCommandContext context,
        string stateDescription)
    {
        var viewCallbackInvoked = false;
        var viewResult = ((ICycleViewQuery)store).Query(
            context,
            _ =>
            {
                viewCallbackInvoked = true;
                return 42;
            });
        Assert.True(
            viewResult is ScopedQueryResult<int>.Unavailable,
            $"Expected the view to be unavailable for {stateDescription}, but got {viewResult.GetType().Name}.");
        Assert.False(viewCallbackInvoked, $"The view callback ran for {stateDescription}.");

        var commandCallbackInvoked = false;
        var commandResult = ((ICycleCommandStore)store).Execute(
            context,
            _ =>
            {
                commandCallbackInvoked = true;
                return 42;
            });
        Assert.True(
            commandResult is ScopedCommandResult<int>.Unavailable,
            $"Expected the command to be unavailable for {stateDescription}, but got {commandResult.GetType().Name}.");
        Assert.False(commandCallbackInvoked, $"The command callback ran for {stateDescription}.");
    }

    private static void AssertPlayerIsRedacted(Player player)
    {
        Assert.Equal("", player.Email);
        Assert.Equal("", player.PasswordHash);
        Assert.Equal("", player.ExternalIssuer);
        Assert.Equal("", player.ExternalSubject);
    }

    private static Guid[] TargetViewIdentifiers(SqlServerCycleScopeFixtureIds ids) =>
    [
        ids.PlayerA,
        ids.PlayerB,
        ids.PlayerC,
        ids.GameA,
        ids.ConfigurationA,
        ids.CycleA,
        ids.EnrolmentAA,
        ids.SectorA,
        ids.SystemA1,
        ids.SystemA2,
        ids.EmpireA1,
        ids.EmpireA2,
        ids.EmpireA3,
        ids.ParticipantA1,
        ids.ParticipantA2,
        ids.MetricA,
        ids.ConstructionA,
        ids.RankingA,
        ids.AdmiralA,
        ids.OutpostA,
        ids.DoctrineA,
        ids.DiplomacyA,
        ids.LinkA,
        ids.FleetA1,
        ids.FleetA2,
        ids.OrderA1,
        ids.OrderA2,
        ids.EventA,
        ids.BattleA,
        ids.ChronicleA,
        ids.MajorEventA,
        ids.SignalA,
        ids.HistoryA
    ];

    private static Guid[] ExcludedViewIdentifiers(ScopedStoreFixture fixture) =>
    [
        fixture.HiddenGameId,
        fixture.Ids.GameB,
        fixture.Ids.ConfigurationB,
        fixture.Ids.ConfigurationBUnused,
        fixture.Ids.CycleB,
        fixture.Ids.EnrolmentAB,
        fixture.Ids.EnrolmentBA,
        fixture.Ids.EnrolmentBB,
        fixture.Ids.SectorB,
        fixture.Ids.SystemB1,
        fixture.Ids.SystemB2,
        fixture.Ids.EmpireB1,
        fixture.Ids.EmpireB2,
        fixture.Ids.EmpireB3,
        fixture.Ids.ParticipantB1,
        fixture.Ids.ParticipantB2,
        fixture.Ids.MetricB,
        fixture.Ids.ConstructionB,
        fixture.Ids.RankingB,
        fixture.Ids.AdmiralB,
        fixture.Ids.AdmiralBUnused,
        fixture.Ids.OutpostB,
        fixture.Ids.DoctrineB,
        fixture.Ids.DiplomacyB,
        fixture.Ids.LinkB,
        fixture.Ids.FleetB1,
        fixture.Ids.FleetB2,
        fixture.Ids.OrderB1,
        fixture.Ids.OrderB2,
        fixture.Ids.EventB,
        fixture.Ids.BattleB,
        fixture.Ids.ChronicleB,
        fixture.Ids.MajorEventB,
        fixture.Ids.SignalB,
        fixture.Ids.HistoryB
    ];

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
            SET Status = N'Pending',
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

    private sealed record CommandAuthorityMutation(
        string Description,
        string ApplySql,
        string RestoreSql,
        Guid Id);

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
