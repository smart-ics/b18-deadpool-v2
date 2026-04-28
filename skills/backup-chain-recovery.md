# Skill: Backup Chain Recovery

## Use When

Use this skill when working on:

- One-Click Restore
- Restore chain logic
- Point-in-time recovery
- Retention safety validation

---

# Core Concept

Restore uses backup chain:

```text
Full → Differential → Transaction Logs
```

Recovery depends on intact chain.

Broken chain = recovery risk.

---

# Restore Rules

## Rule 1
Restore always starts with Full backup.

Always.

---

## Rule 2
If Differential exists:

Restore:

```text
Full
Differential
Required Logs
```

Use latest valid differential before target restore point.

---

## Rule 3
Log backups must restore in exact sequence.

No gaps allowed.

---

# Standard Restore Sequence

## Restore to Latest

```sql
RESTORE DATABASE MyHospital
FROM DISK='Full.bak'
WITH NORECOVERY;

RESTORE DATABASE MyHospital
FROM DISK='Diff.bak'
WITH NORECOVERY;

RESTORE LOG MyHospital
FROM DISK='Log1.trn'
WITH NORECOVERY;

RESTORE LOG MyHospital
FROM DISK='Log2.trn'
WITH RECOVERY;
```

Final step uses:

```sql
WITH RECOVERY
```

only once.

---

# Point-in-Time Restore

Use:

```sql
RESTORE LOG MyHospital
FROM DISK='LogFile.trn'
WITH STOPAT='yyyy-mm-dd hh:mm:ss',
RECOVERY;
```

Used for:

- accidental deletes
- data corruption rollback
- user error recovery

---

# Chain Validation Checklist

Before restore validate:

- Full backup exists
- Differential matches Full
- Log sequence complete
- Required files exist
- Backup naming sequence valid

Only restore validated chains.

---

# Broken Chain Scenarios

## Missing Full Backup
Recovery impossible.

---

## Missing Log File
Can only restore up to last intact log.

---

## Recovery Model Changed
Treat chain broken.
New Full backup required.

---

# Retention Safety Rule

Retention must never remove backups required by restore chain.

Never delete:

- last valid Full backup
- dependent Differential
- required Log sequence

Retention must preserve recoverability.

---

# One-Click Restore Principle

Operator should not determine restore sequence manually.

System should calculate:

```text
Which backups
What order
What restore point
```

Operator clicks Restore.

System handles chain.

---

# Agent Guidance

When generating restore logic:

Always:

- Resolve chain before restore
- Validate chain before execution
- Preserve log sequence ordering
- Fail if chain incomplete

Never:

- Guess missing backups
- Skip chain validation
- Restore logs out of order
- Allow retention to break chain