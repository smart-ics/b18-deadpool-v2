# Coding Instructions

These instructions govern all autonomous coding agents working in this repository.

Follow PRODUCT.md, ARCHITECTURE.md and DECISIONS.md before generating code.

---

# Core Coding Principles

Apply:

- SOLID principles
- Clean Code practices
- Prefer composition over inheritance
- Prefer explicit code over hidden magic
- Keep classes small and focused

Default bias:

Prefer simple solutions first.

Avoid overengineering.

---

# Architecture Rules

Must obey:

- Respect Clean Architecture dependency rule
- No business logic in UI
- Core must not depend on Infrastructure
- Use dependency injection
- Keep domain logic inside Core

Do not violate ADR decisions.

---

# Domain Modeling

Use:

- DDD-lite pragmatic approach

Guidelines:

- Model meaningful domain concepts
- Use value objects where appropriate
- Do not force complex DDD patterns unnecessarily

Pragmatism over purity.

---

# C# Conventions

Use:

- async/await by default
- file-scoped namespaces
- records for value objects
- primary constructors: avoid
- nullable reference types: disabled

Prefer modern C# where it improves clarity.

---

# Data Access Rules

Use:

- Dapper
- Parameterized SQL only
- Repositories for data access

Never:

- Use string-concatenated SQL
- Introduce ORM abstractions
- Hide SQL behavior behind unnecessary layers

Transparency is preferred.

---

# Testing Rules

Testing should be pragmatic.

Use:

- xUnit
- Moq
- FluentAssertions

Follow:

- Arrange Act Assert structure
- BDD-style test naming

Format:

MethodName_ShouldExpectedResult_WhenCondition

Generate tests with production code when appropriate.

Test important logic, not trivial code.

---

# Error Handling Rules

Rules:

- Fail explicitly; never fail silently
- Never swallow exceptions
- Retry only transient failures
- Non-transient failures must stop and alert
- Use domain exceptions for business rule violations
- Use infrastructure exceptions for SQL/IO/network failures
- Log errors with operational context
- Do not use exceptions for control flow

Principle:

Recover what can recover.  
Stop what may cause damage.

---

# Simplicity Rules

Always prefer:

- Simple over clever
- Explicit over abstract
- Built-in over framework-heavy
- Fewer moving parts

Do not introduce frameworks unless justified.

Avoid accidental complexity.

---

# Rules for Coding Agents

Agents must NOT:

- Invent requirements outside PRODUCT.md
- Violate DECISIONS.md
- Introduce libraries without approval
- Bypass architecture boundaries
- Refactor major design decisions without proposal

If requirements are unclear:

Choose conservative implementation aligned with existing decisions.

---

# Code Generation Preference

Generate work in small incremental steps.

Prefer:

1. Implement small slice
2. Show code
3. Explain after code
4. Iterate

Do not generate large speculative code dumps.

Favor working increments.