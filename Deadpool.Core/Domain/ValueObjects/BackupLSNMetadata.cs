namespace Deadpool.Core.Domain.ValueObjects;

/// <summary>
/// LSN (Log Sequence Number) metadata from SQL Server backups.
/// Required for restore chain validation and retention cleanup safety.
/// </summary>
public record BackupLSNMetadata
{
    /// <summary>
    /// First LSN in the backup.
    /// For transaction logs, this must match the LastLSN of the previous backup in the chain.
    /// </summary>
    public decimal? FirstLSN { get; init; }

    /// <summary>
    /// Last LSN in the backup.
    /// For transaction logs, the next log's FirstLSN must match this value.
    /// </summary>
    public decimal? LastLSN { get; init; }

    /// <summary>
    /// Database backup LSN (for Differential backups).
    /// This is the CheckpointLSN of the Full backup that this Differential is based on.
    /// Used to determine which Full backup a Differential depends on.
    /// </summary>
    public decimal? DatabaseBackupLSN { get; init; }

    /// <summary>
    /// Checkpoint LSN (for Full backups).
    /// Differential backups will reference this value in their DatabaseBackupLSN.
    /// </summary>
    public decimal? CheckpointLSN { get; init; }

    public BackupLSNMetadata(
        decimal? firstLSN,
        decimal? lastLSN,
        decimal? databaseBackupLSN,
        decimal? checkpointLSN)
    {
        FirstLSN = firstLSN;
        LastLSN = lastLSN;
        DatabaseBackupLSN = databaseBackupLSN;
        CheckpointLSN = checkpointLSN;
    }
}
