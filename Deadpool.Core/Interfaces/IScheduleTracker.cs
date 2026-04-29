using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Interfaces;

// Records the last UTC time a scheduled backup job was created per (database, type).
// Used to detect whether a new occurrence is due without re-querying the database on
// every poll tick.
public interface IScheduleTracker
{
    DateTime GetLastScheduled(string databaseName, BackupType backupType);
    void MarkScheduled(string databaseName, BackupType backupType, DateTime scheduledAtUtc);
}
