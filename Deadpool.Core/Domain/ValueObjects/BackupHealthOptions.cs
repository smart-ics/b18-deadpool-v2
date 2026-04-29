namespace Deadpool.Core.Domain.ValueObjects;

public class BackupHealthOptions
{
    public TimeSpan FullBackupOverdueThreshold { get; }
    public TimeSpan DifferentialBackupOverdueThreshold { get; }
    public TimeSpan LogBackupOverdueThreshold { get; }
    public TimeSpan ChainLookbackPeriod { get; }

    public BackupHealthOptions(
        TimeSpan fullBackupOverdueThreshold,
        TimeSpan differentialBackupOverdueThreshold,
        TimeSpan logBackupOverdueThreshold,
        TimeSpan chainLookbackPeriod)
    {
        if (fullBackupOverdueThreshold <= TimeSpan.Zero)
            throw new ArgumentException("Full backup overdue threshold must be positive.", nameof(fullBackupOverdueThreshold));

        if (differentialBackupOverdueThreshold <= TimeSpan.Zero)
            throw new ArgumentException("Differential backup overdue threshold must be positive.", nameof(differentialBackupOverdueThreshold));

        if (logBackupOverdueThreshold <= TimeSpan.Zero)
            throw new ArgumentException("Log backup overdue threshold must be positive.", nameof(logBackupOverdueThreshold));

        if (chainLookbackPeriod <= TimeSpan.Zero)
            throw new ArgumentException("Chain lookback period must be positive.", nameof(chainLookbackPeriod));

        FullBackupOverdueThreshold = fullBackupOverdueThreshold;
        DifferentialBackupOverdueThreshold = differentialBackupOverdueThreshold;
        LogBackupOverdueThreshold = logBackupOverdueThreshold;
        ChainLookbackPeriod = chainLookbackPeriod;
    }

    public static BackupHealthOptions Default => new(
        fullBackupOverdueThreshold: TimeSpan.FromHours(26),
        differentialBackupOverdueThreshold: TimeSpan.FromHours(6),
        logBackupOverdueThreshold: TimeSpan.FromMinutes(30),
        chainLookbackPeriod: TimeSpan.FromDays(7)
    );
}
