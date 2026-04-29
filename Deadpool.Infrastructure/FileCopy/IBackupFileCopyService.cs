namespace Deadpool.Infrastructure.FileCopy;

public interface IBackupFileCopyService
{
    Task CopyBackupFileAsync(string sourceFilePath, string databaseName, CancellationToken cancellationToken);
}
