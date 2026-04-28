# Domain Model

This document defines the core business concepts of Deadpool.

Focus:
Shared language for code and design.

---

## Domain Scope

Deadpool manages backup and recovery automation for:

- One production SQL Server database per hospital
- Backup policy execution
- Backup chain management
- Recovery preparation

---

## Core Domain Concepts

### BackupPolicy
Defines backup strategy.

Includes:

- Full backup schedule
- Differential schedule
- Transaction log schedule

Purpose:
Controls how protection is applied.

---

### BackupJob
Represents one executed backup.

States:

- Pending
- Running
- Completed
- Failed

---

### BackupChain
Represents logical restore sequence:

Full → Differential → Logs

Purpose:
Support recoverability.

Broken chain = recovery risk.

---

### RetentionPolicy
Defines:

- How long backups are kept
- Rules for safe cleanup

---

### RecoveryConfiguration
Defines restore-related settings used by One-Click Restore.

Includes:

- Restore target
- File locations
- Restore options

---

## Ubiquitous Language

Use these terms consistently:

- Backup Policy
- Backup Job
- Backup Chain
- Recovery Point
- Retention Policy
- Restore Chain
- Last Backup Status

---

## Core Domain Rules

Rules:

- Differential depends on Full
- Log backups depend on valid chain
- Retention must not break restore chain
- Backup files must follow naming convention
- Backup failure must never be silent

---

## Domain Boundaries

In Scope:

- Backup automation
- Monitoring
- Restore-chain logic

Out of Scope:

- Replication
- Performance tuning
- Enterprise DR features