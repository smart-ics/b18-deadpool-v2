using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Interfaces;

namespace Deadpool.Infrastructure.Scheduling;

// Thread-safe in-memory tracker for the scheduler's last-fired timestamp per
// (database, backup type). State is intentionally lost on restart; the scheduler
// handles that by querying the job repository on first boot to recover the last
// known scheduled time.
public sealed class InMemoryScheduleTracker : IScheduleTracker
{
    private readonly Dictionary<string, DateTime> _state = new();
    private readonly object _lock = new();

    public DateTime GetLastScheduled(string databaseName, BackupType backupType)
    {
        lock (_lock)
        {
            return _state.TryGetValue(Key(databaseName, backupType), out var t) ? t : DateTime.MinValue;
        }
    }

    public void MarkScheduled(string databaseName, BackupType backupType, DateTime scheduledAtUtc)
    {
        lock (_lock)
        {
            _state[Key(databaseName, backupType)] = scheduledAtUtc;
        }
    }

    private static string Key(string db, BackupType t) => $"{db}:{(int)t}";
}
