# Tasks

Current focus:

Phase-1 Backup and Monitoring

Rule:

Work highest priority unfinished task first.

Do not start lower priority tasks while P0 tasks incomplete.

---

## EPIC-1 Backup Foundation

Goal:

Produce reliable automated SQL Server backups.

---

### P0-001 Implement Backup Policy Model

Status: Todo

Build domain model for:

- BackupPolicy
- RetentionPolicy
- BackupType

Done when:

- Domain model implemented
- Validation rules included
- Unit tests pass

Assigned agents:

- Coding
- Testing

---

### P0-002 Implement Full Backup Execution

Status: Todo

Implement Full backup execution via native T-SQL.

Includes:

- Generate backup command
- Execute via Dapper
- Persist BackupJob history

Done when:

- Full backup runs successfully
- Backup file generated
- Metadata stored

Agents:

- Coding
- DBA
- Testing

---

### P0-003 Implement Differential Backup

Status: Todo

Implement differential backup execution.

Depends on:

- P0-002

---

### P0-004 Implement Transaction Log Backup

Status: Todo

Implement transaction log backup execution.

Depends on:

- P0-003

Requires DBA review.

---

## EPIC-2 Scheduling

Goal:

Automate backup execution.

---

### P0-005 Implement Cronos Scheduler

Status: Todo

Implement:

- Read schedules from appsettings
- Parse Cronos expressions
- Trigger due jobs

Done when:

- Scheduled jobs trigger correctly
- Schedule parsing tested

---

### P0-006 Implement Worker Pipeline

Status: Todo

Implement hosted workers:

- BackupSchedulerWorker
- BackupExecutionWorker

Done when:

- Workers run end-to-end

---

## EPIC-3 Backup Storage Copy

Goal:

Copy backups to separate storage server.

---

### P1-007 Implement Backup File Copy

Status: Todo

Implement:

- Copy local backup to network share
- Validate copied file
- Log copy result

Requires:

- Retry handling

---

## EPIC-4 Monitoring

Goal:

Detect problems early.

---

### P1-008 Implement Backup Health Monitoring

Status: Todo

Implement:

- Last backup status
- Backup overdue detection
- Backup chain health

Requires DBA review.

---

### P1-009 Implement Storage Monitoring

Status: Todo

Monitor:

- Backup storage free space
- Low space warnings

---

## EPIC-5 Retention

Goal:

Automate cleanup safely.

---

### P1-010 Implement Retention Cleanup

Status: Todo

Implement:

- Delete expired backup files
- Protect latest full backup
- Preserve restore chain safety

Requires:

- DBA review mandatory

High risk task.

---

## EPIC-6 UI

Goal:

Minimal operational UI.

---

### P2-011 Build Monitoring Dashboard

Status: Todo

Show:

- Last backup status
- Recent jobs
- Storage status

---

### P2-012 Build Backup Job Monitor

Status: Todo

Grid:

- backup history
- job status
- failures

---

## EPIC-7 Restore Foundation (Phase-2 Prep)

Goal:

Prepare one-click restore foundation.

---

### P1-013 Model Backup Chain Resolution

Status: Todo

Given restore point:

Determine required:

- Full backup
- Differential
- Log chain

Requires DBA review.

Important Phase-2 foundation.

---

## Technical Debt Queue

Future improvements:

- Backup compression tuning
- Restore VERIFYONLY integration
- Backup naming validator
- Install/Deployment PowerShell scripts

---

## Agent Working Rules

Before coding each task:

1 Review PRODUCT.md  
2 Review relevant ADRs  
3 Implement smallest working slice  
4 Add/update tests  
5 Request review from required agents

---

## Current Suggested Starting Sequence

Recommended order:

1 P0-001 BackupPolicy model  
2 P0-002 Full Backup execution  
3 P0-005 Cronos Scheduler  
4 P0-006 Worker pipeline  
5 P1-007 Backup file copy

Start here.
