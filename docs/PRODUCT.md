# Deadpool Backup Tools

## Purpose

Deadpool is a lightweight backup automation tool for SQL Server databases in hospitals.

It is designed to help customer hospitals run safer backup policies, reduce recovery risks, and simplify restore operations with minimal operator effort.

Deadpool is an internal engineering utility delivered together with our Hospital Information System, not a standalone commercial backup product.

---

## Problem Statement

Many hospital customers still rely on nightly full database backups.

This creates several problems:

- Risk of large data loss between backups
- Backup storage grows rapidly for large databases (200GB–700GB)
- Full backups can interfere with transactional system performance
- Restore operations may take hours due to very large backup files
- Copying large backup files to safe storage is slow and operationally difficult

Deadpool addresses these problems by implementing a better backup policy:

- Weekly Full Backup
- Daily Differential Backup
- Frequent Transaction Log Backup

This reduces:

- data loss exposure
- storage consumption
- operational impact
- recovery time

---

## Target Environment

Typical deployment assumptions:

- One hospital = one SQL Server instance
- One production monolithic database per hospital
- Other test/dummy databases are out of scope
- Support legacy customer environments, including SQL Server 2014+
- On-premise Windows Server environments
- Backup copy sent to separate backup storage server via network share

Primary user:

- EDP (Hospital IT / Electronic Data Processing staff)

---

## Product Principles

Deadpool follows three principles:

### 1. Minimal Operator Effort

Backup and recovery should require as little manual intervention as possible.

### 2. Simplicity Over Sophistication

Prefer simple, reliable mechanisms over complex enterprise-style solutions.

### 3. Safe Automation

Automation must reduce risk, not introduce risk.

---

## Product Roadmap

### Phase 1 — Backup and Monitoring

Goal:
Automate backup operations based on defined backup policy.

Scope:

- Automated Full / Differential / Transaction Log backup
- Backup scheduling based on policy
- Copy backup files to separate backup storage using network share
- Backup monitoring
- Failure detection
- Configurable retention management

Success means:

- Backup automation works
- Monitoring exists
- Operator knows when backup fails
- Retention cleanup works

---

### Phase 2 — One-Click Restore

Goal:
Restore database with a single action.

Scope:

- Restore requires no manual input
- Restore sequence automatically determined
- All variables stored in configuration
- Operator only clicks Restore

---

### Phase 3 — Auto Recovery

Goal:
System can trigger restore automatically when production database is unavailable.

Possible trigger:

- Database pulse/health check failure

Scope:

- Failure detection
- Automatic execution of One-Click Restore procedure
- Minimal manual intervention

---

## Non Goals

Deadpool is intentionally NOT:

- An enterprise disaster recovery product
- A replication solution
- A cloud backup platform
- A web-based system

It focuses only on practical backup and recovery automation for our HIS deployments.

---

## Current Scope

Current development focus is Phase 1 only.

Priority order:

1. Reliable Backup Automation
2. Monitoring
3. Backup File Copy to Separate Storage
4. Retention Management

Reliable backup comes before advanced automation.
