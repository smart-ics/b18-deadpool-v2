namespace Deadpool.Core.Domain.ValueObjects;

public sealed class BackupProgress
{
    public string BackupType { get; init; } = "FULL";
    public double PercentComplete { get; init; }
    public DateTime StartTime { get; init; }
    public int ElapsedSeconds { get; init; }
    public int RemainingSeconds { get; init; }
}
