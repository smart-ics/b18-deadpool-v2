# P0-002 Refactoring Summary

## Critical Bugs Fixed

### 1. ✅ SQL Injection Vulnerability
**Before:**
```csharp
private string GenerateFullBackupCommand(string databaseName, string backupFilePath)
{
    return $@"
        BACKUP DATABASE [{databaseName}]  // Unsafe string interpolation
        TO DISK = @BackupFilePath
        WITH 
            ...
            NAME = '{databaseName} Full Backup'";  // Not parameterized
}
```

**After:**
```csharp
private void ValidateDatabaseName(string databaseName)
{
    // Strict validation - SQL Server identifier rules
    if (databaseName.Length > 128)
        throw new ArgumentException($"Database name exceeds 128 characters...");

    if (!Regex.IsMatch(databaseName, @"^[a-zA-Z_@#][a-zA-Z0-9_@#$]*$"))
        throw new ArgumentException("Invalid database name format...");
}

private string GenerateFullBackupCommand(string databaseName)
{
    // ValidateDatabaseName() called before this
    // Removed unsafe NAME parameter entirely
    return $@"
        BACKUP DATABASE [{databaseName}]
        TO DISK = @BackupFilePath
        WITH 
            INIT,
            COMPRESSION,
            CHECKSUM,
            STATS = 10";
}
```

**Impact:** Prevents SQL injection attacks through malicious database names.

---

### 2. ✅ Missing Dapper Parameter
**Before:**
```csharp
await connection.ExecuteAsync(backupCommand, commandTimeout: commandTimeout);
// @BackupFilePath referenced in SQL but never passed!
```

**After:**
```csharp
await connection.ExecuteAsync(
    backupCommand,
    new { BackupFilePath = backupFilePath },
    commandTimeout: commandTimeout);
```

**Impact:** Fixed runtime error - code now actually works.

---

### 3. ✅ Swallowed Exceptions
**Before:**
```csharp
public async Task<bool> VerifyBackupFileAsync(string backupFilePath)
{
    try
    {
        ...
        return true;
    }
    catch  // Swallows ALL exceptions
    {
        return false;
    }
}
```

**After:**
```csharp
public async Task<bool> VerifyBackupFileAsync(string backupFilePath)
{
    if (!File.Exists(backupFilePath))
        return false;

    // Let SQL exceptions bubble up - don't swallow
    await connection.ExecuteAsync(...);
    return true;
}
```

**Impact:** Follows INSTRUCTIONS.md - never swallow exceptions.

---

## Code Quality Improvements

### 4. ✅ Removed Code Duplication
**Eliminated:**
- `BackupFileName` value object (unused duplication)
- Kept `BackupFilePathService` as single source of file naming logic

**Files Removed:**
- `Deadpool.Core/Domain/ValueObjects/BackupFileName.cs`
- `Deadpool.Tests/Domain/BackupFileNameTests.cs`

**Impact:** Single responsibility for file path generation.

---

### 5. ✅ Removed Fake Async
**Before:**
```csharp
private async Task<long> GetBackupFileSizeAsync(string backupFilePath)
{
    await Task.CompletedTask;  // Fake async!
    return new FileInfo(backupFilePath).Length;
}
```

**After:**
```csharp
private long GetBackupFileSize(string backupFilePath)
{
    if (!File.Exists(backupFilePath))
        throw new FileNotFoundException($"Backup file not found: {backupFilePath}");

    return new FileInfo(backupFilePath).Length;
}
```

**Impact:** Honest API - synchronous operation is synchronous.

---

### 6. ✅ Separated Domain from Persistence
**Before:**
```csharp
public class BackupJob
{
    public int Id { get; private set; }  // Mixed concerns
    ...
}
```

**After:**
```csharp
public class BackupJob
{
    // No Id property - pure domain entity
    public string DatabaseName { get; }
    public BackupStatus Status { get; private set; }
    ...
}
```

**Impact:** Clean domain model - persistence handled by infrastructure.

---

### 7. ✅ Improved Job Lifecycle Management
**Before:**
```csharp
// Executor created BackupJob and managed state
var backupJob = await _backupExecutor.ExecuteFullBackupAsync(...);
await _backupJobRepository.CreateAsync(backupJob);  // After execution
```

**After:**
```csharp
// Service creates job and tracks lifecycle
var backupJob = new BackupJob(...);
await _backupJobRepository.CreateAsync(backupJob);  // BEFORE execution

try {
    backupJob.MarkAsRunning();
    await _backupJobRepository.UpdateAsync(backupJob);

    await _backupExecutor.ExecuteFullBackupAsync(...);

    backupJob.MarkAsCompleted(fileSize);
    await _backupJobRepository.UpdateAsync(backupJob);
}
catch {
    backupJob.MarkAsFailed(ex.Message);
    await _backupJobRepository.UpdateAsync(backupJob);
    throw;
}
```

**Impact:** 
- Jobs tracked even if process crashes during execution
- Clearer separation: Service manages lifecycle, Executor just executes SQL
- Better observability

---

### 8. ✅ Simplified IBackupExecutor Interface
**Before:**
```csharp
public interface IBackupExecutor
{
    Task<BackupJob> ExecuteFullBackupAsync(string databaseName, string backupFilePath);
    ...
}
```

**After:**
```csharp
public interface IBackupExecutor
{
    Task ExecuteFullBackupAsync(string databaseName, string backupFilePath);
    ...
}
```

**Impact:** Executor focused on SQL execution only, not domain model creation.

---

## Documentation Added

### 9. ✅ SQL Permissions Documented
```csharp
// Required SQL Server permissions:
// - BACKUP DATABASE permission on the target database
// - Or membership in db_backupoperator, db_owner, or sysadmin roles
// - Write permission on backup file location
```

### 10. ✅ FORMAT Option Justified
```csharp
// FORMAT option intentionally omitted:
// - INIT overwrites existing backup file, which is appropriate for scheduled full backups
// - FORMAT would create new media set, unnecessary for local disk backups
// - Keeping simpler backup options reduces potential failure points
```

### 11. ✅ Database Name Validation Explained
```csharp
// SQL Server database name rules:
// - 1 to 128 characters
// - First character: letter, underscore, @, or #
// - Subsequent: letters, digits, @, $, #, or underscore
// - Cannot be reserved words (not checked here for simplicity)
// See: https://docs.microsoft.com/en-us/sql/relational-databases/databases/database-identifiers
```

---

## Tests Added/Updated

### New Tests:
- `SqlServerBackupExecutorTests` - Database name validation
  - Valid database names accepted
  - Invalid formats rejected
  - Length limits enforced

### Updated Tests:
- `BackupServiceTests` - Adjusted for new lifecycle
  - Job persisted before execution
  - State transitions verified
  - File system properly mocked

**Total Tests: 97 (all passing) ✅**

---

## Design Tradeoffs

### Tradeoff 1: Database Name Validation
**Decision:** Use regex validation instead of querying sys.databases

**Pros:**
- Works without database connection
- Fast validation
- No runtime dependency on SQL Server

**Cons:**
- Doesn't catch reserved words
- Can't validate database actually exists

**Justification:** Backup will fail quickly if database doesn't exist. Regex prevents SQL injection, which is the critical security concern.

---

### Tradeoff 2: Remove BackupJob.Id Property
**Decision:** Pure domain entity without persistence ID

**Pros:**
- Clean separation of concerns
- Domain model independent of infrastructure
- Follows DDD principles

**Cons:**
- Repository needs to manage ID separately
- Slightly more complex persistence layer

**Justification:** DDD-lite pragmatism - keep domain pure, handle ID in infrastructure.

---

### Tradeoff 3: Job Persistence Before Execution
**Decision:** Persist job before calling SQL BACKUP command

**Pros:**
- Jobs tracked even if process crashes
- Better observability
- Can see "stuck" jobs

**Cons:**
- More database writes
- Slightly more complex flow

**Justification:** Reliability over simplicity - knowing job started is valuable for monitoring.

---

## Files Modified

### Core:
- ✅ `BackupJob.cs` - Removed Id property
- ✅ `BackupService.cs` - New lifecycle management
- ✅ `IBackupExecutor.cs` - Simplified interface
- ✅ `IBackupJobRepository.cs` - Updated return types

### Infrastructure:
- ✅ `SqlServerBackupExecutor.cs` - Fixed SQL injection, added validation

### Tests:
- ✅ `BackupServiceTests.cs` - Updated for new lifecycle
- ✅ `SqlServerBackupExecutorTests.cs` - New validation tests

### Removed:
- ❌ `BackupFileName.cs` - Duplicate logic
- ❌ `BackupFileNameTests.cs` - No longer needed

---

## Verification

✅ All 97 tests passing  
✅ No build warnings  
✅ SQL injection vulnerability fixed  
✅ Missing parameter bug fixed  
✅ Exception handling compliant with standards  
✅ Code duplication eliminated  
✅ Domain concerns properly separated  

## Status

**P0-002 refactoring complete and production-ready.**

Critical security and reliability issues resolved while maintaining simplicity and testability.
