# Configuration Guide

This document explains how to configure Deadpool after the shared-configuration refactor.

Deadpool now uses three JSON configuration files:

- `appsettings.shared.json`: shared source of truth for data used by both Agent and UI
- `Deadpool.Agent/appsettings.json`: Agent-only settings
- `Deadpool.UI/appsettings.json`: UI-only settings

## Configuration Model

### Shared file

`appsettings.shared.json` contains values that must be consistent across both applications.

Current sections:

- `DeadpoolDb`
- `BackupPolicies`

### Agent file

`Deadpool.Agent/appsettings.json` contains background worker and infrastructure settings.

Current sections:

- `Logging`
- `ConnectionStrings`
- `ExecutionWorker`
- `BackupCopy`
- `HealthMonitoring`
- `StorageMonitoring`
- `DatabasePulse`

### UI file

`Deadpool.UI/appsettings.json` contains dashboard display settings only.

Current sections:

- `Dashboard`
- `Logging`

## How Discovery Works

Both executables start from their own output directory and walk upward until they find `appsettings.shared.json`.

That means this layout works correctly:

```text
D:\Deadpool\
  appsettings.shared.json
  deadpool.db
  Agent\
    Deadpool.Agent.exe
    appsettings.json
  UI\
    Deadpool.UI.exe
    appsettings.json
```

If `appsettings.shared.json` cannot be found above the executable location, startup fails.

## Setup Steps

### 1. Create the root deployment folder

Example:

```text
D:\Deadpool
```

### 2. Create the shared SQLite path

Set `DeadpoolDb:Path` to a location both applications can access.

Example:

```json
{
  "DeadpoolDb": {
    "Path": "D:\\Deadpool\\deadpool.db"
  }
}
```

Recommendations:

- Put the file outside the Agent and UI subfolders
- Use a stable path that survives redeployments
- Ensure the Agent service account has read/write permission
- Ensure the UI user has at least read permission

### 3. Define shared backup policies

Each object in `BackupPolicies` represents one monitored production database.

Fields:

- `DatabaseName`: SQL Server database name
- `RecoveryModel`: display value used by the UI and policy formatting
- `FullBackupCron`: full backup schedule
- `DifferentialBackupCron`: differential backup schedule
- `TransactionLogBackupCron`: log backup schedule
- `RetentionDays`: how long backups should be kept
- `BootstrapFullBackupEnabled`: optional flag that allows startup bootstrapping behavior

Example:

```json
{
  "BackupPolicies": [
    {
      "DatabaseName": "MyHospitalDB",
      "RecoveryModel": "Full",
      "FullBackupCron": "0 0 * * 0",
      "DifferentialBackupCron": "0 1 * * 1-6",
      "TransactionLogBackupCron": "*/15 * * * *",
      "RetentionDays": 14,
      "BootstrapFullBackupEnabled": true
    }
  ]
}
```

Cron format used here is 5-part:

```text
minute hour day month weekday
```

Examples:

- `0 0 * * 0`: every Sunday at 00:00
- `0 1 * * 1-6`: every Monday-Saturday at 01:00
- `*/15 * * * *`: every 15 minutes

### 4. Configure the Agent SQL connection

The Agent needs a SQL Server connection string in:

- `ConnectionStrings:ProductionDatabase`

Example using Windows authentication:

```json
{
  "ConnectionStrings": {
    "ProductionDatabase": "Server=SQLPROD01;Database=master;Integrated Security=True;TrustServerCertificate=True;"
  }
}
```

Example using SQL authentication:

```json
{
  "ConnectionStrings": {
    "ProductionDatabase": "Server=SQLPROD01;Database=master;User Id=deadpool;Password=StrongPasswordHere;TrustServerCertificate=True;"
  }
}
```

Notes:

- The Agent currently connects to SQL Server using this connection string
- The configured SQL login must be able to back up the target databases
- The database pulse worker also uses this connection string to probe connectivity

### 5. Configure stale-job handling

`ExecutionWorker:StaleJobThreshold` controls when a running job is considered abandoned.

Example:

```json
{
  "ExecutionWorker": {
    "StaleJobThreshold": "02:00:00"
  }
}
```

Use a larger value if backups are expected to run for a long time.

### 6. Configure backup copy behavior

`BackupCopy` controls optional backup-file copying after backup execution.

Fields:

- `RemoteStoragePath`: destination folder or UNC share
- `MaxRetryAttempts`: retry count for transient copy failures
- `RetryDelay`: delay between retries

Example with UNC share:

```json
{
  "BackupCopy": {
    "RemoteStoragePath": "\\\\BackupServer\\SqlBackups",
    "MaxRetryAttempts": 3,
    "RetryDelay": "00:00:05"
  }
}
```

To effectively disable copy behavior, leave `RemoteStoragePath` empty.

### 7. Configure backup health monitoring

`HealthMonitoring` controls how the Agent evaluates backup freshness.

Fields:

- `CheckInterval`: how often the health worker runs
- `FullBackupOverdueThreshold`: full backup warning/critical boundary input
- `DifferentialBackupOverdueThreshold`: differential backup threshold
- `LogBackupOverdueThreshold`: log backup threshold
- `ChainLookbackPeriod`: how far back to inspect the chain
- `HealthCheckRetentionDays`: retention in SQLite for backup health rows

Example:

```json
{
  "HealthMonitoring": {
    "CheckInterval": "00:05:00",
    "FullBackupOverdueThreshold": "26:00:00",
    "DifferentialBackupOverdueThreshold": "06:00:00",
    "LogBackupOverdueThreshold": "00:30:00",
    "ChainLookbackPeriod": "7.00:00:00",
    "HealthCheckRetentionDays": 7
  }
}
```

### 8. Configure storage monitoring

`StorageMonitoring` is now the only storage-threshold configuration source.

Fields:

- `CheckInterval`
- `WarningThresholdPercentage`
- `CriticalThresholdPercentage`
- `MinimumWarningFreeSpaceGB`
- `MinimumCriticalFreeSpaceGB`
- `HealthCheckRetentionDays`
- `MonitoredVolumes`

Example:

```json
{
  "StorageMonitoring": {
    "CheckInterval": "00:10:00",
    "WarningThresholdPercentage": 20,
    "CriticalThresholdPercentage": 10,
    "MinimumWarningFreeSpaceGB": 50,
    "MinimumCriticalFreeSpaceGB": 20,
    "HealthCheckRetentionDays": 7,
    "MonitoredVolumes": [
      "D:\\Deadpool\\Backups",
      "\\\\BackupServer\\SqlBackups"
    ]
  }
}
```

How it works:

- The Agent checks each volume listed in `MonitoredVolumes`
- Health results are written into the shared SQLite database
- The UI reads the latest stored storage health from SQLite

Important:

- The UI still uses `Dashboard:BackupVolumePath` to decide which stored volume record to display
- Make sure that UI `Dashboard:BackupVolumePath` matches one entry from Agent `StorageMonitoring:MonitoredVolumes`

### 9. Configure database pulse monitoring

`DatabasePulse` controls SQL Server connectivity checks performed by the Agent.

Fields:

- `CheckInterval`
- `RetentionDays`

Example:

```json
{
  "DatabasePulse": {
    "CheckInterval": "00:01:00",
    "RetentionDays": 7
  }
}
```

How it works:

- The Agent probes SQL Server connectivity on the schedule above
- Results are written into shared SQLite
- The UI reads the latest pulse result and displays it without probing SQL directly

### 10. Configure the UI dashboard

`Dashboard` contains only display and selection settings.

Fields:

- `DatabaseName`: which database policy the UI should display
- `BackupVolumePath`: which monitored storage path the UI should show
- `AutoRefreshIntervalSeconds`: refresh cadence for dashboard polling

Example:

```json
{
  "Dashboard": {
    "DatabaseName": "MyHospitalDB",
    "BackupVolumePath": "D:\\Deadpool\\Backups",
    "AutoRefreshIntervalSeconds": 60
  }
}
```

Important:

- `DatabaseName` should match one `BackupPolicies[].DatabaseName`
- `BackupVolumePath` should match one `StorageMonitoring:MonitoredVolumes[]`
- The UI no longer uses a SQL Server connection string

## Recommended Production Example

### appsettings.shared.json

```json
{
  "DeadpoolDb": {
    "Path": "D:\\Deadpool\\deadpool.db"
  },
  "BackupPolicies": [
    {
      "DatabaseName": "MyHospitalDB",
      "RecoveryModel": "Full",
      "FullBackupCron": "0 0 * * 0",
      "DifferentialBackupCron": "0 1 * * 1-6",
      "TransactionLogBackupCron": "*/15 * * * *",
      "RetentionDays": 14,
      "BootstrapFullBackupEnabled": true
    }
  ]
}
```

### Deadpool.Agent/appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "ConnectionStrings": {
    "ProductionDatabase": "Server=SQLPROD01;Database=master;Integrated Security=True;TrustServerCertificate=True;"
  },
  "ExecutionWorker": {
    "StaleJobThreshold": "02:00:00"
  },
  "BackupCopy": {
    "RemoteStoragePath": "\\\\BackupServer\\SqlBackups",
    "MaxRetryAttempts": 3,
    "RetryDelay": "00:00:05"
  },
  "HealthMonitoring": {
    "CheckInterval": "00:05:00",
    "FullBackupOverdueThreshold": "26:00:00",
    "DifferentialBackupOverdueThreshold": "06:00:00",
    "LogBackupOverdueThreshold": "00:30:00",
    "ChainLookbackPeriod": "7.00:00:00",
    "HealthCheckRetentionDays": 7
  },
  "StorageMonitoring": {
    "CheckInterval": "00:10:00",
    "WarningThresholdPercentage": 20,
    "CriticalThresholdPercentage": 10,
    "MinimumWarningFreeSpaceGB": 50,
    "MinimumCriticalFreeSpaceGB": 20,
    "HealthCheckRetentionDays": 7,
    "MonitoredVolumes": [
      "D:\\Deadpool\\Backups"
    ]
  },
  "DatabasePulse": {
    "CheckInterval": "00:01:00",
    "RetentionDays": 7
  }
}
```

### Deadpool.UI/appsettings.json

```json
{
  "Dashboard": {
    "DatabaseName": "MyHospitalDB",
    "BackupVolumePath": "D:\\Deadpool\\Backups",
    "AutoRefreshIntervalSeconds": 60
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

## Common Misconfigurations

### Shared file not found

Symptom:

- Agent or UI fails on startup saying it cannot locate `appsettings.shared.json`

Fix:

- Place `appsettings.shared.json` above both executable folders
- Keep the Agent and UI under the same root folder tree

### UI shows no storage data

Symptom:

- Dashboard loads but storage status is missing or warning-only

Fix:

- Ensure Agent `StorageMonitoring:MonitoredVolumes` contains the path
- Ensure UI `Dashboard:BackupVolumePath` exactly matches that same path
- Ensure Agent is running and writing storage health rows to SQLite

### UI shows no pulse data

Symptom:

- Database pulse status is unknown

Fix:

- Ensure Agent is running
- Ensure `ConnectionStrings:ProductionDatabase` is valid
- Ensure `DatabasePulse` section exists in Agent config

### UI shows wrong database policy

Symptom:

- Dashboard policy does not match the intended database

Fix:

- Ensure UI `Dashboard:DatabaseName` matches one shared `BackupPolicies[].DatabaseName`

### SQLite permission problems

Symptom:

- Agent or UI fails to read/write the shared DB

Fix:

- Grant write permissions to the Agent account on the directory containing `deadpool.db`
- Grant read permissions to the UI user
- Confirm the path in `DeadpoolDb:Path` exists and is valid

## Operational Recommendation

For production, keep these values aligned:

- Shared `BackupPolicies[].DatabaseName`
- UI `Dashboard:DatabaseName`
- Agent `StorageMonitoring:MonitoredVolumes[]`
- UI `Dashboard:BackupVolumePath`
- Shared `DeadpoolDb:Path`

When those values are aligned, the Agent becomes the single source of truth and the UI remains a pure read model.