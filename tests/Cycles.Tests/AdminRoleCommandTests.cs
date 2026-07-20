using Cycles.Application;

namespace Cycles.Tests;

public sealed class AdminRoleCommandTests
{
    [Fact]
    public void Command_normalises_a_bounded_reason()
    {
        var command = new AdminRoleCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AdminRoleChangeKind.Grant,
            "  On-call operator.  ",
            TestState.Now);

        Assert.Equal("On-call operator.", command.Reason);
    }

    [Fact]
    public void Command_rejects_invalid_identifiers_change_and_reason()
    {
        Assert.Throws<ArgumentException>(() => new AdminRoleCommand(
            Guid.Empty,
            Guid.NewGuid(),
            AdminRoleChangeKind.Grant,
            "On-call operator.",
            TestState.Now));
        Assert.Throws<ArgumentException>(() => new AdminRoleCommand(
            Guid.NewGuid(),
            Guid.Empty,
            AdminRoleChangeKind.Grant,
            "On-call operator.",
            TestState.Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AdminRoleCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            (AdminRoleChangeKind)999,
            "On-call operator.",
            TestState.Now));
        Assert.Throws<ArgumentException>(() => new AdminRoleCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AdminRoleChangeKind.Grant,
            " ",
            TestState.Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AdminRoleCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AdminRoleChangeKind.Grant,
            new string('x', AdminRoleCommand.MaximumReasonLength + 1),
            TestState.Now));
    }
}
