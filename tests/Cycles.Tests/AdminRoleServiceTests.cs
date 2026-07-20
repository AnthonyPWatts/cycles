using Cycles.Core;

namespace Cycles.Tests;

public sealed class AdminRoleServiceTests
{
    [Fact]
    public void Active_admin_can_grant_and_revoke_with_audit_records()
    {
        var state = TestState.CreateSingleEmpireState();
        var actor = state.Players.Single();
        actor.Role = PlayerRole.Admin;
        var target = new Player
        {
            Username = "target",
            Status = PlayerStatus.Active,
            CreatedAt = TestState.Now
        };
        state.Players.Add(target);

        var grant = AdminRoleService.Grant(state, actor.PlayerId, target.PlayerId, "On-call operator.", TestState.Now);
        var revoke = AdminRoleService.Revoke(state, actor.PlayerId, target.PlayerId, "Rotation ended.", TestState.Now.AddDays(1));

        Assert.Equal(PlayerRole.Player, target.Role);
        Assert.Equal(AdminRoleAuditAction.Granted, grant.Action);
        Assert.Equal(AdminRoleAuditAction.Revoked, revoke.Action);
        Assert.All(state.AdminRoleAuditRecords, audit => Assert.Equal(EventSeverity.High, audit.Severity));
    }

    [Fact]
    public void Routine_operation_cannot_revoke_final_active_admin()
    {
        var state = TestState.CreateSingleEmpireState();
        var admin = state.Players.Single();
        admin.Role = PlayerRole.Admin;

        var exception = Assert.Throws<InvalidOperationException>(() => AdminRoleService.Revoke(
            state,
            admin.PlayerId,
            admin.PlayerId,
            "No longer required.",
            TestState.Now));

        Assert.Contains("final active administrator", exception.Message, StringComparison.Ordinal);
        Assert.Equal(PlayerRole.Admin, admin.Role);
        Assert.Empty(state.AdminRoleAuditRecords);
    }

    [Fact]
    public void Role_change_requires_an_active_admin_and_reason()
    {
        var state = TestState.CreateSingleEmpireState();
        var player = state.Players.Single();
        var target = new Player { Username = "target", Status = PlayerStatus.Active, CreatedAt = TestState.Now };
        state.Players.Add(target);

        Assert.Throws<InvalidOperationException>(() => AdminRoleService.Grant(
            state,
            player.PlayerId,
            target.PlayerId,
            "Not authorised.",
            TestState.Now));

        player.Role = PlayerRole.Admin;
        Assert.Throws<InvalidOperationException>(() => AdminRoleService.Grant(
            state,
            player.PlayerId,
            target.PlayerId,
            " ",
            TestState.Now));
    }

    [Fact]
    public void Routine_role_rules_require_a_human_actor_and_target()
    {
        var actor = new Player
        {
            Kind = PlayerKind.AI,
            Role = PlayerRole.Admin,
            Status = PlayerStatus.Active
        };
        var target = new Player
        {
            Kind = PlayerKind.Human,
            Role = PlayerRole.Player,
            Status = PlayerStatus.Active
        };

        Assert.Equal(
            AdminRoleRuleFailure.ActorIsNotActiveHumanAdministrator,
            AdminRoleService.EvaluateGrant(actor, target));

        actor.Kind = PlayerKind.Human;
        target.Kind = PlayerKind.AI;

        Assert.Equal(
            AdminRoleRuleFailure.TargetIsAutomated,
            AdminRoleService.EvaluateGrant(actor, target));
    }

    [Fact]
    public void Automated_admin_does_not_satisfy_the_final_active_human_admin_guard()
    {
        var state = TestState.CreateSingleEmpireState();
        var humanAdmin = state.Players.Single();
        humanAdmin.Role = PlayerRole.Admin;
        state.Players.Add(new Player
        {
            Username = "automated-admin",
            Kind = PlayerKind.AI,
            Role = PlayerRole.Admin,
            Status = PlayerStatus.Active,
            CreatedAt = TestState.Now
        });

        var exception = Assert.Throws<InvalidOperationException>(() => AdminRoleService.Revoke(
            state,
            humanAdmin.PlayerId,
            humanAdmin.PlayerId,
            "No longer required.",
            TestState.Now));

        Assert.Contains("final active administrator", exception.Message, StringComparison.Ordinal);
        Assert.Equal(PlayerRole.Admin, humanAdmin.Role);
        Assert.Empty(state.AdminRoleAuditRecords);
    }
}
