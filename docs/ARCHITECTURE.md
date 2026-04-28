# Architecture

## Overview

Deadpool is designed as a lightweight backup automation tool using a simple layered architecture with a background agent and desktop configuration UI.

Architecture priorities:

1. Reliability
2. Simplicity
3. Safe automation
4. Future support for restore automation (Phase-2)

---

## Solution Structure

```text
Deadpool.UI
Deadpool.Agent
Deadpool.Core
Deadpool.Infrastructure
Deadpool.Tests
```

## Responsibilities

### Deadpool.UI

WinForms application for:

- Configuration
- Monitoring dashboard
- Manual operations (future restore trigger)

Approach:

- Pragmatic WinForms
- Simple code-behind
- Keep business logic out of UI

---

### Deadpool.Agent

.NET Worker Service running as Windows Service.

Responsible for:

- Backup scheduling
- Backup execution
- Monitoring
- Retention cleanup

Uses multiple hosted workers.

Proposed workers:

```text
BackupSchedulerWorker
BackupExecutionWorker
MonitoringWorker
RetentionWorker
```

---

### Deadpool.Core

Domain and application core.

Contains:

- Domain models
- Business rules
- Interfaces
- Backup policies
- Restore orchestration logic (Phase-2 ready)

Core has no infrastructure dependencies.

---

### Deadpool.Infrastructure

Infrastructure implementations:

- Dapper repositories
- SQL Server backup execution
- SQLite metadata repository
- File copy services
- Logging integration

---

### Deadpool.Tests

Testing project for:

- Domain tests
- Integration tests
- Backup chain logic tests

---

## Runtime Architecture

### Primary Components

Deployment uses:

1 Production SQL Server  
1 Backup Storage Server  
1 Deadpool Agent host

Flow:

```text
Production Database
   ↓
Local Backup File
   ↓
Copy to Backup Storage Server
```

Backup is always created locally first, then copied to separate storage.

Rationale:

- Safer for large files
- Less network risk during backup creation
- Copy failures isolated from backup generation

---

## Scheduling Architecture

Scheduling engine uses:

- Cronos

Reason:

- Lightweight
- Simple cron scheduling
- Suitable for background worker model

Scheduling configuration stored in:

- appsettings.json

Scheduling values remain configurable, not hardcoded.

## Schedule Configuration Management

Backup schedules are configuration-driven.

Schedule definitions are maintained in:

- appsettings.json

Schedules are intentionally not editable through the WinForms UI.

Rationale:

- Keep UI simple and focused on monitoring
- Reduce application complexity
- Configuration changes are rare
- Intended users (EDP staff) can manage configuration files directly

UI may display configured schedules as read-only for monitoring purposes.

---

## Backup Execution Strategy

Backup execution uses:

- Native SQL Server BACKUP commands
- Executed via Dapper

Backup strategy supports:

- Full Backup
- Differential Backup
- Transaction Log Backup

Restore-chain compatibility is considered in backup design because Phase-2 follows immediately after Phase-1.

## Backup File Naming Convention

Backup files follow a deterministic naming convention to support:

- easy identification
- restore chain sequencing
- manual inspection
- automated restore logic (Phase-2)

Pattern:

```text
{DatabaseName}_FULL_{yyyyMMdd}_{HHmm}.bak
{DatabaseName}_DIFF_{yyyyMMdd}_{HHmm}.bak
{DatabaseName}_LOG_{yyyyMMdd}_{HHmm}.trn
```

Example:

```text
MyHospital_FULL_20260428_0200.bak
MyHospital_DIFF_20260429_0300.bak
MyHospital_LOG_20260429_0915.trn
```

Naming convention is considered part of restore-chain architecture and should remain stable.

---

## Metadata Repository

Deadpool maintains lightweight internal metadata storage using:

- SQLite

Stores:

- Backup jobs
- Schedule definitions
- Backup history
- Monitoring status
- Alerts
- Restore chain metadata (Phase-2)

SQLite chosen for simplicity and embedded deployment.

---

## Monitoring Scope

Phase-1 monitoring includes:

### Backup Status

- Last successful backup
- Backup failures
- Backup overdue warnings

### Backup Chain Health

- Full/Differential/Log chain validity
- Detect missing chain segments

### Storage Monitoring

- Backup storage disk capacity
- Low-space warning

---

## Domain Model

Core domain concepts:

```text
BackupPolicy
BackupJob
BackupChain
RetentionPolicy
RecoveryConfiguration
```

These form the domain language used across solution layers.

---

## Logging

Logging uses:

- Serilog

Logging targets may include:

- Rolling file logs
- Optional structured logs in metadata repository

Log important events:

- Backup start/finish
- Retry attempts
- Failures
- Monitoring warnings
- Retention cleanup
- Restore actions (Phase-2)

---

## Failure Handling Philosophy

Approach:

- Automatic retry on transient failures
- Default retry policy:
  3 attempts
- If still failing:
  stop and alert operator

Principle:

Retry what may recover.
Alert what needs humans.

---

## Phase Compatibility

Architecture intentionally supports:

### Phase-1

Backup automation + monitoring

### Phase-2

One-click restore

Restore support is considered in architecture from the beginning because backup-chain handling depends on it.

Phase-3 Auto Recovery is intentionally excluded from current architectural scope.

---

## Architectural Decisions

Key decisions:

- WinForms over WPF (Phase-1 pragmatism)
- .NET Worker Service over SQL Agent
- Multiple hosted workers over single worker
- Cronos over Quartz.NET
- SQLite for internal metadata
- Local backup first, then copy to storage
- Native T-SQL backup commands via Dapper
- Serilog for logging

See DECISIONS.md for ADR details.
