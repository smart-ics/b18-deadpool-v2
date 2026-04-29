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

Status: Done

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

Status: Done

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

Status: Done

Implement differential backup execution.

Depends on:

- P0-002

---

### P0-004 Implement Transaction Log Backup

Status: Done

Implement transaction log backup execution.

Depends on:

- P0-003

Requires DBA review.

---

### P0-R1 Refactor Common Backup Execution Pattern
Status: Done

Goal:
Eliminate duplication across:

- Full backup execution
- Differential backup execution
- Transaction log backup execution

Reason:
Created from Architect review after P0-004.

Done when:
- Common backup lifecycle extracted
- Duplication materially reduced
- No behavior changes
- All tests pass

Agents:
- Coding
- Architect
- DBA (review)

---

## EPIC-2 Scheduling

Goal:

Automate backup execution.

---

### P0-005 Implement Cronos Scheduler

Status: Done

Implement:

- Read schedules from appsettings
- Parse Cronos expressions
- Trigger due jobs

Done when:

- Scheduled jobs trigger correctly
- Schedule parsing tested

---

### P0-006 Implement Worker Pipeline

Status: Done

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

Status: Done

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

## EPIC-8 Operational Readiness Enhancements

Goal:

Improve operational safety and operator visibility for production deployment readiness.

---

### P1-014 Bootstrap Initial Full Backup

Status: Todo

Implement startup behavior:

- Detect when no valid Full backup chain exists
- Automatically trigger bootstrap Full backup
- After bootstrap, continue normal backup schedule

Rules:

- Full backup bootstrap occurs once when chain not initialized
- Differential and Log backups must not run before valid Full exists
- Surface "Backup Chain Initialized" status

Requires:
- DBA review mandatory

High priority safety enhancement.

---

### P2-015 Display Backup Policy in Plain English

Status: Todo

Enhance UI dashboard to display configured backup policy in operator-friendly language.

Show:

- Full backup schedule in plain English
- Differential backup schedule in plain English
- Log backup schedule in plain English
- Recovery model
- Retention policy

Example:

- Full Backup runs at midnight every Sunday
- Differential Backup runs at midnight Monday through Saturday
- Transaction Log Backup runs every 15 minutes

Focus:
- No raw cron expressions shown to operators

---

### P2-016 Show Database and Backup Topology Status

Status: Todo

Display configuration and topology information in UI:

Show:

- Production database name
- SQL Server address / instance
- Backup storage server location
- Connection endpoints summary

Goal:

Operator can verify what is being protected.

---

### P1-017 Production Database Pulse Monitoring

Status: Todo

Implement monitoring for production database availability.

Monitor:

- Database reachable/unreachable
- Simple database pulse check
- Database online status

Display:

- Healthy
- Warning
- Critical

Constraints:

- Simple availability monitoring only
- Not a SQL performance monitoring tool

Requires:
- DBA review

---

### P2-018 Show Backup Chain Initialization Status

Status: Todo

Display in dashboard:

- Backup Chain Initialized: Yes/No
- Last valid Full backup
- Restore chain currently healthy/unhealthy

Warn clearly if system not yet protected.

Depends on:
- P1-014


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
