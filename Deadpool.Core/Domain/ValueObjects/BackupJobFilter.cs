using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Domain.ValueObjects;

/// <summary>
/// Filter criteria for backup job history queries.
/// </summary>
public record BackupJobFilter
{
    public string? DatabaseName { get; init; }
    public BackupType? BackupType { get; init; }
    public BackupStatus? Status { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int MaxResults { get; init; } = 100;

    public BackupJobFilter()
    {
    }

    public BackupJobFilter(string? databaseName)
    {
        DatabaseName = databaseName;
    }
}

/// <summary>
/// Display model for backup job in monitoring grid.
/// </summary>
public record BackupJobDisplayModel
{
    public string DatabaseName { get; init; }
    public string BackupType { get; init; }
    public string Status { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public string Duration { get; init; }
    public string FilePath { get; init; }
    public long? FileSizeBytes { get; init; }
    public string? ErrorMessage { get; init; }

    public BackupJobDisplayModel(BackupJob job)
    {
        DatabaseName = job.DatabaseName;
        BackupType = job.BackupType.ToString();
        Status = job.Status.ToString();
        StartTime = job.StartTime;
        EndTime = job.EndTime;
        Duration = job.GetDuration()?.ToString(@"hh\:mm\:ss") ?? "--";
        FilePath = job.BackupFilePath;
        FileSizeBytes = job.FileSizeBytes;
        ErrorMessage = job.ErrorMessage;
    }

    public string FileSizeDisplay => FileSizeBytes.HasValue 
        ? FormatBytes(FileSizeBytes.Value) 
        : "--";

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
