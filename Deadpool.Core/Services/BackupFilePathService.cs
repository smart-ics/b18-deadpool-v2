using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Services;

public class BackupFilePathService
{
    private readonly string _backupDirectory;

    public BackupFilePathService(string backupDirectory)
    {
        if (string.IsNullOrWhiteSpace(backupDirectory))
            throw new ArgumentException("Backup directory cannot be empty.", nameof(backupDirectory));

        _backupDirectory = backupDirectory;
    }

    public string GenerateBackupFilePath(string databaseName, BackupType backupType)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

        var timestamp = DateTime.UtcNow;
        var backupTypeString = backupType switch
        {
            BackupType.Full => "FULL",
            BackupType.Differential => "DIFF",
            BackupType.TransactionLog => "LOG",
            _ => throw new ArgumentException($"Unsupported backup type: {backupType}", nameof(backupType))
        };

        var dateString = timestamp.ToString("yyyyMMdd");
        var timeString = timestamp.ToString("HHmm");
        var extension = backupType == BackupType.TransactionLog ? "trn" : "bak";
        var fileName = $"{databaseName}_{backupTypeString}_{dateString}_{timeString}.{extension}";

        return Path.Combine(_backupDirectory, fileName);
    }
}
