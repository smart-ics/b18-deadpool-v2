# Skill: SQL Server Backup

## Use When

Use this skill when working on:

- Backup execution logic
- Backup policy implementation
- Backup verification
- Troubleshooting backup failures

---

# Default Backup Policy

Deadpool default policy:

```text
Weekly Full Backup
Daily Differential Backup
Transaction Log Backup every 15 minutes
```

Purpose:

- Reduce storage growth
- Reduce recovery time
- Reduce data loss exposure

---

# Core Backup Rules

## Rule 1
Full backup is foundation.

No differential or log backup should exist without a valid full backup.

---

## Rule 2
Differential backup depends on latest full backup.

---

## Rule 3
Transaction log backups require:

- FULL recovery model
- Existing valid full backup
- Unbroken log chain

---

## Rule 4
Backup is created:

1 Local disk first  
2 Then copied to backup storage

Never reverse this order.

---

# Standard T-SQL Commands

## Full Backup

```sql
BACKUP DATABASE [MyHospital]
TO DISK = 'D:\Backup\MyHospital_FULL_yyyymmdd_hhmm.bak'
WITH COMPRESSION, CHECKSUM;
```

---

## Differential Backup

```sql
BACKUP DATABASE [MyHospital]
TO DISK = 'D:\Backup\MyHospital_DIFF_yyyymmdd_hhmm.bak'
WITH DIFFERENTIAL, COMPRESSION, CHECKSUM;
```

---

## Transaction Log Backup

```sql
BACKUP LOG [MyHospital]
TO DISK = 'D:\Backup\MyHospital_LOG_yyyymmdd_hhmm.trn'
WITH COMPRESSION, CHECKSUM;
```

---

# Backup Verification

Always validate backups:

```sql
RESTORE VERIFYONLY
FROM DISK='D:\Backup\MyHospital_FULL.bak';
```

Principle:

A backup not verified is an assumption.

---

# Backup File Naming Convention

Use:

```text
{Database}_FULL_{yyyyMMdd}_{HHmm}.bak
{Database}_DIFF_{yyyyMMdd}_{HHmm}.bak
{Database}_LOG_{yyyyMMdd}_{HHmm}.trn
```

Naming convention supports restore chain logic.

Do not change casually.

---

# Common Failure Modes

## Transient (Retry Allowed)

- Temporary connection loss
- Network share unavailable
- Temporary file lock

Retry up to configured retry policy.

---

## Non-Transient (Stop + Alert)

- Disk full
- Permission denied
- Broken recovery model assumptions
- Backup command failure due to invalid state

Stop and alert operator.

---

# Agent Guidance

When generating backup code:

Always:

- Use native T-SQL BACKUP commands
- Use Dapper
- Use parameterized SQL
- Preserve backup chain safety
- Prefer reliability over optimization

Never:

- Break backup chain assumptions
- Skip verification logic
- Generate direct network-share-only backups