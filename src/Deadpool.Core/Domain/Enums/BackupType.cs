namespace Deadpool.Core.Domain.Enums;

/// <summary>
/// Types of SQL Server backups
/// </summary>
public enum BackupType
{
    /// <summary>
    /// Full database backup - contains all data and allows standalone restore
    /// </summary>
    Full = 1,

    /// <summary>
    /// Differential backup - contains changes since last full backup
    /// </summary>
    Differential = 2,

    /// <summary>
    /// Transaction log backup - contains log records for point-in-time recovery
    /// </summary>
    Log = 3
}
