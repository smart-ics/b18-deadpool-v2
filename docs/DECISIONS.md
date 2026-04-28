# Architecture Decisions

This document captures major architectural decisions for Deadpool.

Format:

- Context
- Decision
- Rationale
- Consequences

---

## ADR-001  : Use Clean Architecture Dependency Rule

### Context

Deadpool consists of UI, background agents, domain logic, and infrastructure concerns.

### Decision

Use layered architecture with dependency flowing inward:

```text
UI → Core
Agent → Core
Infrastructure → Core
```

Core has no dependency on outer layers.

### Rationale

Maintain separation of concerns while keeping architecture pragmatic.

### Consequences

- Better maintainability
- Easier testing
- Slightly more structure up front

---

## ADR-002 : Use WinForms for Desktop UI

### Decision

Use WinForms for Phase-1 desktop application.

### Rationale

Based on existing expertise and faster delivery.

Administrative tools do not require rich UI complexity.

### Consequences

- Faster development
- Lower learning curve
- WPF deferred

---

## ADR-003 :  Use .NET Worker Service

### Decision

Use .NET Worker Service as backup agent runtime.

### Rationale

Keep Deadpool as all-in-one solution without dependency on external schedulers or tools.

### Consequences

- Self-contained deployment
- More control
- Background processing handled inside solution

---

## ADR-004 : Use Cronos for Scheduling

### Decision

Use Cronos for schedule handling.

### Rationale

- Lightweight
- Sufficient for current needs
- Opportunity to adopt simpler scheduling model than Quartz
- Supports learning goal

### Consequences

- Less overhead than Quartz
- Fewer advanced scheduler features (acceptable)

---

## ADR-005 : Use SQLite for Internal Metadata

### Decision

Use SQLite as internal metadata repository.

### Rationale

Portable, structured, queryable, lightweight.

### Consequences

- Embedded deployment
- No extra server dependency
- Simple operational footprint

---

## ADR-006 : Standardize Backup Policy

### Decision

Default backup policy:

- Weekly Full
- Daily Differential
- Frequent Transaction Log Backup

### Rationale

Minimize user choices.
Favor simple defaults over excessive configurability.

### Consequences

- Easier adoption
- Lower operator confusion
- Opinionated product behavior

---

## ADR-007 : Backup Locally First, Then Copy to Storage

### Decision

Backup files are created locally first before copied to storage server.

### Rationale

More reliable backup creation and faster backup execution.

### Consequences

- Better resilience against network problems
- Requires temporary local disk capacity

---

## ADR-008 : Use Configuration Files Instead of Schedule UI Editing

### Decision

Schedules managed through appsettings.json.

Not editable from UI.

### Rationale

Tool is intended for advanced users (EDP), not beginner users.

Keep UI simpler.

### Consequences

- Simpler application
- Less UI complexity
- Schedule changes require config edits

---

## ADR-009  : Retry Limited Times Then Alert

### Decision

Retry transient failures up to 3 times, then stop and alert operator.

### Rationale

Prevent endless self-healing loops.
Allow operators to diagnose and learn root causes.

### Consequences

- Controlled automation
- Requires operator intervention after repeated failures

---

## ADR-010 : Design Architecture for Phase-1 and Phase-2

### Decision

Current architecture explicitly supports:

- Backup Automation
- One-Click Restore

Phase-3 Auto Recovery deferred.

### Rationale

Backup chain restore is new for EDP and needs tooling support now.

Auto recovery can remain manual-assisted for now.

### Consequences

- Better focus
- Avoid premature complexity
- Phase-3 remains future evolution

---

## ADR-011 : Use Dapper for SQL Access

### Decision

Use Dapper rather than EF Core or SMO.

### Rationale

Prefer transparent and explicit data access.
Avoid abstraction that hides optimization opportunities.

### Consequences

- More control
- More handwritten SQL
- Better fit for infrastructure-oriented tool
