# AppSettings Samples

This document contains sample configuration files for the three JSON files used by Deadpool:

- `appsettings.shared.json`
- `Deadpool.Agent/appsettings.json`
- `Deadpool.UI/appsettings.json`

These samples match the current configuration model used by the codebase.

---

## Portable Deployment Layout

Recommended folder structure:

```text
D:\Deadpool\
  appsettings.shared.json
  deadpool.db
  Backups\
  Agent\
    Deadpool.Agent.exe
    appsettings.json
  UI\
    Deadpool.UI.exe
    appsettings.json
```

The Agent and UI both walk upward from their executable directory until they find `appsettings.shared.json`.

---

## Sample appsettings.shared.json

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

---

## Sample Deadpool.Agent/appsettings.json

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
      "D:\\Deadpool\\Backups",
      "\\\\BackupServer\\SqlBackups"
    ]
  },
  "DatabasePulse": {
    "CheckInterval": "00:01:00",
    "RetentionDays": 7
  }
}
```

---

## Sample Deadpool.UI/appsettings.json

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

---

## Minimal Local Dev Samples

Use these if you want to start with the smallest working setup.

### Shared

```json
{
  "DeadpoolDb": {
    "Path": "D:\\Deadpool\\deadpool.db"
  },
  "BackupPolicies": [
    {
      "DatabaseName": "dev",
      "RecoveryModel": "Full",
      "FullBackupCron": "0 0 * * 0",
      "DifferentialBackupCron": "0 0 * * 1-6",
      "TransactionLogBackupCron": "*/15 * * * *",
      "RetentionDays": 14
    }
  ]
}
```

### Agent

```json
{
  "ConnectionStrings": {
    "ProductionDatabase": "Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;"
  },
  "ExecutionWorker": {
    "StaleJobThreshold": "02:00:00"
  },
  "BackupCopy": {
    "RemoteStoragePath": "",
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

### UI

```json
{
  "Dashboard": {
    "DatabaseName": "dev",
    "BackupVolumePath": "D:\\Deadpool\\Backups",
    "AutoRefreshIntervalSeconds": 60
  }
}
```