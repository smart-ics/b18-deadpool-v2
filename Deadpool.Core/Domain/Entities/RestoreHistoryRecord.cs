namespace Deadpool.Core.Domain.Entities;

public sealed class RestoreHistoryRecord
{
    public long Id { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public DateTime RestoreTimestamp { get; set; }
    public DateTime TargetRestoreTime { get; set; }
    public string FullBackupFile { get; set; } = string.Empty;
    public string? DiffBackupFile { get; set; }
    public IReadOnlyList<string> LogBackupFiles { get; set; } = Array.Empty<string>();
    public bool Success { get; set; }
    public long DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
}
