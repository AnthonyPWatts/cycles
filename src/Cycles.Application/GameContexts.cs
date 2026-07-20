namespace Cycles.Application;

[Flags]
public enum GamePermission
{
    None = 0,
    Read = 1 << 0,
    Organise = 1 << 1,
    Administer = 1 << 2
}

public sealed record GameAccessContext
{
    private const GamePermission AllPermissions =
        GamePermission.Read |
        GamePermission.Organise |
        GamePermission.Administer;

    public GameAccessContext(
        Guid playerId,
        Guid gameId,
        Guid? gameEnrolmentId,
        GamePermission permissions)
    {
        if (playerId == Guid.Empty)
        {
            throw new ArgumentException("Player identifier cannot be empty.", nameof(playerId));
        }

        if (gameId == Guid.Empty)
        {
            throw new ArgumentException("Game identifier cannot be empty.", nameof(gameId));
        }

        if (gameEnrolmentId == Guid.Empty)
        {
            throw new ArgumentException("Game enrolment identifier cannot be empty.", nameof(gameEnrolmentId));
        }

        if ((permissions & ~AllPermissions) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(permissions),
                permissions,
                "Game permissions contain an unsupported flag.");
        }

        if (!permissions.HasFlag(GamePermission.Read))
        {
            throw new ArgumentException(
                "A Game access context must include read permission.",
                nameof(permissions));
        }

        PlayerId = playerId;
        GameId = gameId;
        GameEnrolmentId = gameEnrolmentId;
        Permissions = permissions;
    }

    public Guid PlayerId { get; }

    public Guid GameId { get; }

    public Guid? GameEnrolmentId { get; }

    public GamePermission Permissions { get; }
}

public sealed record GameCommandContext
{
    public GameCommandContext(
        GameAccessContext gameAccess,
        Guid cycleId,
        Guid matchParticipantId,
        Guid empireId)
    {
        ArgumentNullException.ThrowIfNull(gameAccess);

        if (!gameAccess.GameEnrolmentId.HasValue)
        {
            throw new ArgumentException(
                "A Game command context requires a durable Game enrolment.",
                nameof(gameAccess));
        }

        if (cycleId == Guid.Empty)
        {
            throw new ArgumentException("Cycle identifier cannot be empty.", nameof(cycleId));
        }

        if (matchParticipantId == Guid.Empty)
        {
            throw new ArgumentException(
                "Match participant identifier cannot be empty.",
                nameof(matchParticipantId));
        }

        if (empireId == Guid.Empty)
        {
            throw new ArgumentException("Empire identifier cannot be empty.", nameof(empireId));
        }

        GameAccess = gameAccess;
        CycleId = cycleId;
        MatchParticipantId = matchParticipantId;
        EmpireId = empireId;
    }

    public GameAccessContext GameAccess { get; }

    public Guid CycleId { get; }

    public Guid MatchParticipantId { get; }

    public Guid EmpireId { get; }

    public GameCycleScope Scope => new(GameAccess.GameId, CycleId);
}
