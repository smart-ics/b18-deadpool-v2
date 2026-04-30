using Deadpool.Agent.Infrastructure;
using Deadpool.Core.Interfaces;
using Deadpool.Infrastructure.BackupExecution;
using Deadpool.Infrastructure.Metadata;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deadpool.Tests.Unit;

public class ProductionSqlRuntimeFactoryTests
{
    [Fact]
    public void CreateBackupExecutor_ShouldReturnSqlServerExecutor_WhenConnectionStringConfigured()
    {
        // Arrange
        const string connectionString = "Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;";

        // Act
        var executor = ProductionSqlRuntimeFactory.CreateBackupExecutor(connectionString, NullLogger.Instance);

        // Assert
        executor.Should().BeOfType<SqlServerBackupExecutor>();
    }

    [Fact]
    public void CreateBackupExecutor_ShouldReturnStubExecutor_WhenConnectionStringMissing()
    {
        // Act
        var executor = ProductionSqlRuntimeFactory.CreateBackupExecutor(null, NullLogger.Instance);

        // Assert
        executor.Should().BeOfType<StubBackupExecutor>();
    }

    [Fact]
    public void CreateDatabaseMetadataService_ShouldReturnSqlMetadataService_WhenConnectionStringConfigured()
    {
        // Arrange
        const string connectionString = "Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;";

        // Act
        var metadataService = ProductionSqlRuntimeFactory.CreateDatabaseMetadataService(connectionString, NullLogger.Instance);

        // Assert
        metadataService.Should().BeOfType<SqlServerDatabaseMetadataService>();
    }

    [Fact]
    public void CreateDatabaseMetadataService_ShouldReturnStubMetadataService_WhenConnectionStringMissing()
    {
        // Act
        var metadataService = ProductionSqlRuntimeFactory.CreateDatabaseMetadataService(string.Empty, NullLogger.Instance);

        // Assert
        metadataService.Should().BeOfType<StubDatabaseMetadataService>();
    }
}
