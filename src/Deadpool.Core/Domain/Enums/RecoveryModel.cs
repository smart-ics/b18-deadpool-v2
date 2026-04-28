namespace Deadpool.Core.Domain.Enums;

/// <summary>
/// SQL Server recovery model for a database
/// </summary>
public enum RecoveryModel
{
    /// <summary>
    /// Simple recovery - minimal log retention, no point-in-time recovery
    /// </summary>
    Simple = 1,

    /// <summary>
    /// Full recovery - complete log retention, supports point-in-time recovery
    /// </summary>
    Full = 2,

    /// <summary>
    /// Bulk-logged recovery - minimal logging for bulk operations
    /// </summary>
    BulkLogged = 3
}
