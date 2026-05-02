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

Status: Done

Implement:

- Last backup status
- Backup overdue detection
- Backup chain health

Requires DBA review.

---

### P1-009 Implement Storage Monitoring

Status: Done

Monitor:

- Backup storage free space
- Low space warnings

---

## EPIC-5 Retention

Goal:

Automate cleanup safely.

---

### P1-010 Implement Retention Cleanup

Status: Done

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

Status: Done

Show:

- Last backup status
- Recent jobs
- Storage status

---

### P2-012 Build Backup Job Monitor

Status: Done

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

Status: Done

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

Status: Done

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

Status: Done

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

Status: Done

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

Status: Done

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

Status: Done

Display in dashboard:

- Backup Chain Initialized: Yes/No
- Last valid Full backup
- Restore chain currently healthy/unhealthy

Warn clearly if system not yet protected.

Depends on:

- P1-014

## EPIC-9 Restore Execution (Phase-2)

Goal:

Enable safe, verifiable, and operator-controlled database restore from backups.

Focus:

Restore correctness
Operator safety
Clear restore visibility
Controlled execution (no accidental data loss)

### P1-019 Restore Plan Builder

GOAL
Given a target DateTime (STOPAT), determine:

Required FULL backup
Optional DIFFERENTIAL backup
Required LOG backup chain
Return a RestorePlan object.

INPUT
DateTime targetTime

OUTPUT
RestorePlan:

FullBackup
DifferentialBackup (optional)
LogBackups (ordered)
TargetTime

### P1-020 Restore Plan Validation

Status: Todo

Validate restore plan before execution.

Implement:

Verify all backup files exist on storage
Verify file accessibility (permissions, locks)
Re-validate backup chain integrity (defensive check)
Ensure STOPAT is covered by available backups

Output:

RestoreValidationResult
IsValid
Errors
Warnings

Rules:

Restore must NOT proceed if validation fails
Validation must be deterministic and reproducible

Requires:

DBA review mandatory

### P1-021 Restore Script Builder

Status: Todo

Generate SQL Server restore script from RestorePlan.

Implement:

FULL restore WITH NORECOVERY
DIFF restore WITH NORECOVERY (if exists)
LOG chain restore WITH NORECOVERY
Final LOG restore WITH STOPAT + RECOVERY

Ensure:

Correct ordering
Correct STOPAT placement
No missing steps

Output:

RestoreScript (string or structured steps)

Requires:

DBA review mandatory

### P1-022 Restore Execution Service

Status: Todo

Execute restore script safely.

Implement:

Execute restore commands sequentially via SQL connection
Capture execution result per step
Log all execution steps

Output:

RestoreExecutionResult
Success / Failure
Step logs
Error message (if any)

Constraints:

Must NOT silently overwrite database
Must fail clearly on any step error

### P1-023 Restore Safety Guard

Status: Todo

Prevent accidental destructive restore operations.

Implement:

Explicit confirmation required before execution
Display target database name clearly
Display overwrite warning
Optional: require operator to type database name to confirm

Goal:

Ensure operator intentionally performs restore

### P2-024 Restore Dialog UI

Status: Todo

Implement dedicated Restore interface.

Design:

Separate window (modal), NOT part of dashboard

Features:

Select restore point (DateTime)
Generate restore plan
Display restore plan
Show validation result
Execute restore (after confirmation)

Flow:

Select → Plan → Validate → Confirm → Execute

### P2-025 Restore Plan Visualization

Status: Todo

Display restore plan in operator-friendly format.

Show:

Full backup used
Differential backup (if any)
Ordered log chain
STOPAT position

Format:

Sequential flow (FULL → DIFF → LOG → STOPAT)

Goal:

Operator can understand restore sequence in seconds

### P2-026 Restore Execution Result & History

Status: Todo

Record and display restore activity.

Store:

Restore timestamp
Target restore time
Backups used
Execution result
Duration

Display:

Recent restore history
Success / failure status

Goal:

Provide audit trail and operator confidence

<!-- 
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

Start here. -->
