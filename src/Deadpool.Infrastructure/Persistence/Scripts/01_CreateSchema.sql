-- Deadpool Backup Tools - Database Schema
-- This script creates the core database schema for managing SQL Server backups

USE master;
GO

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'DeadpoolDB')
BEGIN
    CREATE DATABASE DeadpoolDB;
END
GO

USE DeadpoolDB;
GO

-- SQL Server Instances Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SqlServerInstances')
BEGIN
    CREATE TABLE SqlServerInstances (
        Id UNIQUEIDENTIFIER PRIMARY KEY,
        ServerName NVARCHAR(255) NOT NULL,
        InstanceName NVARCHAR(255) NULL,
        Port INT NOT NULL DEFAULT 1433,
        ConnectionString NVARCHAR(1000) NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        Description NVARCHAR(500) NULL,
        LastContactedAt DATETIME2 NULL,
        Version NVARCHAR(100) NULL,
        Edition NVARCHAR(100) NULL,
        CreatedAt DATETIME2 NOT NULL,
        ModifiedAt DATETIME2 NULL,
        CreatedBy NVARCHAR(100) NOT NULL,
        ModifiedBy NVARCHAR(100) NULL,
        CONSTRAINT UQ_SqlServerInstance UNIQUE (ServerName, InstanceName)
    );

    CREATE INDEX IX_SqlServerInstances_IsActive ON SqlServerInstances(IsActive);
END
GO

-- Databases Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Databases')
BEGIN
    CREATE TABLE Databases (
        Id UNIQUEIDENTIFIER PRIMARY KEY,
        SqlServerInstanceId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(255) NOT NULL,
        RecoveryModel TINYINT NOT NULL, -- 1=Simple, 2=Full, 3=BulkLogged
        IsSystemDatabase BIT NOT NULL DEFAULT 0,
        SizeInBytes BIGINT NOT NULL DEFAULT 0,
        LastBackupDate DATETIME2 NULL,
        LastLogBackupDate DATETIME2 NULL,
        IsOnline BIT NOT NULL DEFAULT 1,
        Collation NVARCHAR(128) NULL,
        CompatibilityLevel INT NULL,
        CreatedAt DATETIME2 NOT NULL,
        ModifiedAt DATETIME2 NULL,
        CreatedBy NVARCHAR(100) NOT NULL,
        ModifiedBy NVARCHAR(100) NULL,
        CONSTRAINT FK_Database_SqlServerInstance FOREIGN KEY (SqlServerInstanceId) 
            REFERENCES SqlServerInstances(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_Database UNIQUE (SqlServerInstanceId, Name)
    );

    CREATE INDEX IX_Databases_SqlServerInstanceId ON Databases(SqlServerInstanceId);
    CREATE INDEX IX_Databases_LastBackupDate ON Databases(LastBackupDate);
END
GO

-- Backup Schedules Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BackupSchedules')
BEGIN
    CREATE TABLE BackupSchedules (
        Id UNIQUEIDENTIFIER PRIMARY KEY,
        DatabaseId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(255) NOT NULL,
        BackupType TINYINT NOT NULL, -- 1=Full, 2=Differential, 3=Log
        IsEnabled BIT NOT NULL DEFAULT 1,
        CronExpression NVARCHAR(100) NOT NULL,
        BackupPathTemplate NVARCHAR(500) NOT NULL,
        RetentionDays INT NOT NULL DEFAULT 7,
        IsCompressed BIT NOT NULL DEFAULT 1,
        IsEncrypted BIT NOT NULL DEFAULT 0,
        MaxRetryAttempts INT NOT NULL DEFAULT 3,
        NextRunTime DATETIME2 NULL,
        LastRunTime DATETIME2 NULL,
        LastBackupJobId UNIQUEIDENTIFIER NULL,
        Description NVARCHAR(500) NULL,
        CreatedAt DATETIME2 NOT NULL,
        ModifiedAt DATETIME2 NULL,
        CreatedBy NVARCHAR(100) NOT NULL,
        ModifiedBy NVARCHAR(100) NULL,
        CONSTRAINT FK_BackupSchedule_Database FOREIGN KEY (DatabaseId) 
            REFERENCES Databases(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_BackupSchedules_DatabaseId ON BackupSchedules(DatabaseId);
    CREATE INDEX IX_BackupSchedules_NextRunTime ON BackupSchedules(NextRunTime) WHERE IsEnabled = 1;
END
GO

-- Backup Jobs Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BackupJobs')
BEGIN
    CREATE TABLE BackupJobs (
        Id UNIQUEIDENTIFIER PRIMARY KEY,
        DatabaseId UNIQUEIDENTIFIER NOT NULL,
        BackupScheduleId UNIQUEIDENTIFIER NULL,
        BackupType TINYINT NOT NULL, -- 1=Full, 2=Differential, 3=Log
        Status TINYINT NOT NULL, -- 0=Pending, 1=Running, 2=Completed, 3=Failed, 4=Cancelled, 5=CompletedWithWarnings
        ScheduledStartTime DATETIME2 NOT NULL,
        ActualStartTime DATETIME2 NULL,
        CompletedTime DATETIME2 NULL,
        BackupFilePath NVARCHAR(1000) NOT NULL,
        BackupSizeInBytes BIGINT NULL,
        ErrorMessage NVARCHAR(MAX) NULL,
        RetryCount INT NULL,
        IsCompressed BIT NOT NULL DEFAULT 1,
        IsEncrypted BIT NOT NULL DEFAULT 0,
        CompressionPercentage INT NULL,
        Duration INT NULL, -- Duration in seconds
        CreatedAt DATETIME2 NOT NULL,
        ModifiedAt DATETIME2 NULL,
        CreatedBy NVARCHAR(100) NOT NULL,
        ModifiedBy NVARCHAR(100) NULL,
        CONSTRAINT FK_BackupJob_Database FOREIGN KEY (DatabaseId) 
            REFERENCES Databases(Id) ON DELETE CASCADE,
        CONSTRAINT FK_BackupJob_BackupSchedule FOREIGN KEY (BackupScheduleId) 
            REFERENCES BackupSchedules(Id) ON DELETE NO ACTION
    );

    CREATE INDEX IX_BackupJobs_DatabaseId ON BackupJobs(DatabaseId);
    CREATE INDEX IX_BackupJobs_Status ON BackupJobs(Status);
    CREATE INDEX IX_BackupJobs_ScheduledStartTime ON BackupJobs(ScheduledStartTime);
    CREATE INDEX IX_BackupJobs_CompletedTime ON BackupJobs(CompletedTime);
END
GO

PRINT 'Deadpool database schema created successfully';
