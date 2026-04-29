namespace Deadpool.Core.Domain.ValueObjects;

/// <summary>
/// Defines backup retention policy.
/// Conservative: Prefer retention over deletion when uncertain.
/// </summary>
public record RetentionPolicy
{
    /// <summary>
    /// Maximum age of Full backups to retain.
    /// Backups older than this may be candidates for deletion IF chain safety allows.
    /// </summary>
    public TimeSpan FullBackupRetention { get; }

    /// <summary>
    /// Maximum age of Differential backups to retain.
    /// Backups older than this may be candidates for deletion IF not needed by retained Full backup.
    /// </summary>
    public TimeSpan DifferentialBackupRetention { get; }

    /// <summary>
    /// Maximum age of Log backups to retain.
    /// Backups older than this may be candidates for deletion IF not needed by retained chain.
    /// </summary>
    public TimeSpan LogBackupRetention { get; }

    public RetentionPolicy(
        TimeSpan fullBackupRetention,
        TimeSpan differentialBackupRetention,
        TimeSpan logBackupRetention)
    {
        if (fullBackupRetention <= TimeSpan.Zero)
            throw new ArgumentException("Full backup retention must be positive.", nameof(fullBackupRetention));

        if (differentialBackupRetention <= TimeSpan.Zero)
            throw new ArgumentException("Differential backup retention must be positive.", nameof(differentialBackupRetention));

        if (logBackupRetention <= TimeSpan.Zero)
            throw new ArgumentException("Log backup retention must be positive.", nameof(logBackupRetention));

        // Validation: Differential should not be retained longer than Full
        if (differentialBackupRetention > fullBackupRetention)
            throw new ArgumentException("Differential retention cannot exceed Full backup retention.");

        // Validation: Log should not be retained longer than Differential
        if (logBackupRetention > differentialBackupRetention)
            throw new ArgumentException("Log retention cannot exceed Differential backup retention.");

        FullBackupRetention = fullBackupRetention;
        DifferentialBackupRetention = differentialBackupRetention;
        LogBackupRetention = logBackupRetention;
    }

    public static RetentionPolicy Default => new(
        fullBackupRetention: TimeSpan.FromDays(30),
        differentialBackupRetention: TimeSpan.FromDays(14),
        logBackupRetention: TimeSpan.FromDays(7));
}
