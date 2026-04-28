# Deadpool - SQL Server Backup Automation

## Solution Structure

This solution follows Clean Architecture principles with the following projects:

### Deadpool.Core
Domain models, business rules, and interfaces. No infrastructure dependencies.

**Current Implementation:**
- `BackupType` enum (Full, Differential, TransactionLog)
- `RecoveryModel` enum (Simple, Full, BulkLogged)
- `BackupSchedule` value object for cron expressions
- `BackupPolicy` entity (immutable):
  - Scheduling for all backup types
  - Recovery model awareness
  - Integrated retention policy (always preserves restore chain)
  - Immutable after construction (configuration-driven)

### Deadpool.Infrastructure
Infrastructure implementations for data access, SQL Server backup execution, and file operations.

### Deadpool.Agent
.NET Worker Service that runs as a Windows Service for backup automation.

### Deadpool.UI
WinForms desktop application for configuration and monitoring.

### Deadpool.Tests
xUnit test project with FluentAssertions for domain and integration tests.

**Current Coverage:**
- 35 passing tests for BackupPolicy domain model
- Tests for BackupType, RecoveryModel, BackupSchedule, and BackupPolicy
- Coverage includes domain invariants and restore chain protection
- Immutability validation

## Project References

Following clean architecture dependency rules:
- Infrastructure → Core
- Agent → Core + Infrastructure  
- UI → Core + Infrastructure
- Tests → Core + Infrastructure

Core has no dependencies on outer layers.

## Domain Rules Enforced

1. **Recovery Model Validation**
   - Simple recovery model: Transaction log backups not allowed
   - Full/BulkLogged: Transaction log backup schedule required

2. **Restore Chain Protection (Always Enabled)**
   - Last full backup cannot be deleted
   - Full backups newer than last full preserved
   - Differential and log backups after last full preserved
   - Old backups from previous restore chains can be cleaned up

3. **Immutability**
   - All BackupPolicy properties are immutable
   - Configuration-driven design (loaded once at startup)
   - No runtime mutation methods

## Technology Stack

- .NET 8.0
- xUnit + FluentAssertions for testing
- Planned: Dapper, Cronos, SQLite

## Refactoring Applied

**First Refactor (Post-Review #1):**
1. ✅ Merged RetentionPolicy into BackupPolicy (composition, not separate entity)
2. ✅ Added RecoveryModel awareness
3. ✅ Enforced domain invariants for transaction log backups
4. ✅ Fixed retention logic to preserve restore chains correctly
5. ✅ Removed `IsEnabled` flag (not needed for Phase-1)

**Second Refactor (Post-Review #2 - Simplification):**
1. ✅ Removed `PreserveRestoreChain` parameter (always true, not configurable)
2. ✅ Removed `UpdateRecoveryModel()` method (configuration-driven, not runtime mutable)
3. ✅ Removed `UpdateRetention()` method (configuration-driven, not runtime mutable)
4. ✅ Made all properties immutable (get-only, set via constructor only)
5. ✅ Aligned with ADR-006: Simple defaults over excessive configurability

**Result:** Minimal, focused, immutable domain model following configuration-driven design.

## Status

**Completed:**
- ✅ P0-001: Implement Backup Policy Model (Simplified & Immutable)
- ✅ P0-002: Implement Full Backup Execution
- ✅ P0-003: Implement Differential Backup Execution

**Next:**
- P0-004: Implement Transaction Log Backup Execution
