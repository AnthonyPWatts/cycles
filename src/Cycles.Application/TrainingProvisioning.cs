namespace Cycles.Application;

public sealed record TrainingGameProvisioningCommand
{
    public TrainingGameProvisioningCommand(
        Guid playerId,
        Guid requestId,
        DateTimeOffset requestedAt)
    {
        if (playerId == Guid.Empty)
        {
            throw new ArgumentException("Player identifier cannot be empty.", nameof(playerId));
        }
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Training provisioning request identifier cannot be empty.", nameof(requestId));
        }

        PlayerId = playerId;
        RequestId = requestId;
        RequestedAt = requestedAt;
    }

    public Guid PlayerId { get; }

    public Guid RequestId { get; }

    public DateTimeOffset RequestedAt { get; }
}

public sealed record TrainingGameProvisioningSnapshot(
    Guid TutorialRunId,
    Guid GameId,
    Guid CycleId,
    bool Created);

public interface ITrainingGameProvisioningStore
{
    TrainingGameProvisioningResult ProvisionTwinReaches(
        TrainingGameProvisioningCommand command);
}

public abstract record TrainingGameProvisioningResult
{
    private TrainingGameProvisioningResult()
    {
    }

    public sealed record Success(TrainingGameProvisioningSnapshot Value) : TrainingGameProvisioningResult;

    public sealed record Unavailable() : TrainingGameProvisioningResult;

    public sealed record Busy() : TrainingGameProvisioningResult;
}
