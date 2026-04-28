# Deadpool Backup Tools

**Professional SQL Server Backup and Disaster Recovery Solution for Hospitals**

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-Proprietary-red)](LICENSE)
[![Status](https://img.shields.io/badge/status-Phase%201%20Development-yellow)](docs/TASKS.md)

## 🎯 Overview

Deadpool Backup Tools is an enterprise-grade SQL Server backup and disaster recovery platform designed specifically for hospital IT environments. It provides automated backup scheduling, health monitoring, and (in Phase 2) one-click restore capabilities—all while maintaining HIPAA compliance and reliability standards.

**Current Status:** Phase 1 Development (Backup & Monitoring)

## ✨ Key Features

### Phase 1 (Current)
- ✅ **Automated Backup Scheduling** - Cron-based full, differential, and transaction log backups
- ✅ **Health Monitoring** - Continuous monitoring of backup status and SQL Server health
- ✅ **Windows Service Agent** - Background processing with retry logic and logging
- ✅ **Windows Forms UI** - Desktop application for configuration and monitoring
- ✅ **Production-Grade Architecture** - DDD-Lite clean architecture with Result pattern

### Phase 2 (Planned)
- 🔲 **One-Click Restore** - Automated backup chain validation and restore sequencing
- 🔲 **Point-in-Time Recovery** - Calculate and restore to specific timestamps
- 🔲 **Restore Preview** - Show required backup files before restore

### Phase 3 (Future)
- 🔲 **Auto Recovery** - Detect and automatically recover failed databases
- 🔲 **Cloud Backup** - Azure Blob Storage and AWS S3 integration
- 🔲 **Email/SMS Alerts** - Configurable notifications for failures and warnings

## 🏗️ Architecture

Deadpool follows **DDD-Lite Clean Architecture** principles for maintainability and testability:

```
┌─────────────────────────────────────────────────────────┐
│                     Presentation Layer                   │
│  ┌─────────────────┐              ┌─────────────────┐   │
│  │  Deadpool.UI    │              │  Deadpool.Agent │   │
│  │   (WinForms)    │              │ (Worker Service)│   │
│  └────────┬────────┘              └────────┬────────┘   │
└───────────┼─────────────────────────────────┼───────────┘
            │                                 │
┌───────────┴─────────────────────────────────┴───────────┐
│                   Application Layer                      │
│  ┌──────────────────────────────────────────────────┐   │
│  │            Deadpool.Core                          │   │
│  │  • Domain Entities (SqlServerInstance, Database,  │   │
│  │    BackupJob, BackupSchedule)                     │   │
│  │  • Repository Interfaces                          │   │
│  │  • Service Interfaces                             │   │
│  └──────────────────────────────────────────────────┘   │
└──────────────────────────┬───────────────────────────────┘
                           │
┌──────────────────────────┴───────────────────────────────┐
│                  Infrastructure Layer                     │
│  ┌──────────────────────────────────────────────────┐   │
│  │        Deadpool.Infrastructure                    │   │
│  │  • Dapper Repositories (SqlServerInstanceRepo,    │   │
│  │    DatabaseRepo, BackupJobRepo, etc.)             │   │
│  │  • SQL Server Services (BackupExecution,          │   │
│  │    Monitoring, Scheduler)                         │   │
│  └──────────────────────────────────────────────────┘   │
└───────────────────────────────────────────────────────────┘
```

**Technology Stack:**
- **.NET 8.0** - Latest LTS version of .NET
- **Dapper** - Lightweight, high-performance data access
- **Microsoft.Data.SqlClient** - SQL Server connectivity
- **Serilog** - Structured logging to file and console
- **Windows Forms** - Rapid UI development for Phase 1
- **xUnit + Moq** - Testing framework (90%+ coverage goal)

## 📁 Project Structure

```
Deadpool/
├── src/
│   ├── Deadpool.Core/              # Domain layer (entities, interfaces)
│   │   ├── Domain/
│   │   │   ├── Common/            # Base classes (Entity, ValueObject, Result)
│   │   │   ├── Entities/          # Rich domain entities
│   │   │   └── Enums/             # Domain enumerations
│   │   └── Application/
│   │       ├── Repositories/      # Repository interfaces
│   │       └── Services/          # Service interfaces
│   │
│   ├── Deadpool.Infrastructure/    # Infrastructure implementations
│   │   ├── Persistence/
│   │   │   ├── Repositories/      # Dapper repository implementations
│   │   │   └── Scripts/           # SQL schema creation scripts
│   │   └── SqlServer/             # SQL Server-specific services
│   │
│   ├── Deadpool.Agent/             # Windows Service worker
│   │   ├── Workers/               # Background workers (Scheduler, Executor, Monitor)
│   │   ├── Program.cs             # Host builder with DI
│   │   └── appsettings.json       # Configuration
│   │
│   └── Deadpool.UI/                # Windows Forms UI
│       ├── Forms/                 # UI forms
│       ├── Program.cs             # WinForms entry point
│       └── appsettings.json       # Configuration
│
├── docs/                           # Comprehensive documentation
│   ├── PRODUCT.md                 # Product vision and roadmap
│   ├── ARCHITECTURE.md            # Architecture decisions and diagrams
│   ├── DECISIONS.md               # Architectural Decision Records (ADRs)
│   ├── AGENTS.md                  # AI agent roles and responsibilities
│   ├── INSTRUCTIONS.md            # Coding standards and conventions
│   ├── DOMAIN.md                  # Domain model and ubiquitous language
│   ├── TASKS.md                   # Current tasks and backlog
│   └── TEST_STRATEGY.md           # Testing approach and guidelines
│
├── skills/                         # Domain-specific knowledge for AI agents
│   ├── sql-server-backup.md       # SQL Server backup expertise
│   ├── backup-chain-recovery.md   # Restore and recovery knowledge
│   └── dotnet-worker-service.md   # Worker service patterns
│
├── Deadpool.sln                    # Visual Studio solution
├── .gitignore                      # Git ignore rules
└── README.md                       # This file
```

## 🚀 Getting Started

### Prerequisites

- **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **SQL Server 2016+** - Any edition (Express, Standard, Enterprise)
- **Visual Studio 2022** (recommended) or Visual Studio Code
- **Windows 10/11 or Windows Server 2016+**

### Installation

#### 1. Clone Repository
```bash
git clone https://github.com/your-org/deadpool-backup-tools.git
cd deadpool-backup-tools
```

#### 2. Create DeadpoolDB Database
```bash
# Connect to SQL Server and create database
sqlcmd -S localhost -Q "CREATE DATABASE DeadpoolDB"

# Run schema creation script
sqlcmd -S localhost -d DeadpoolDB -i src\Deadpool.Infrastructure\Persistence\Scripts\01_CreateSchema.sql
```

#### 3. Configure Connection Strings

Edit `src/Deadpool.Agent/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DeadpoolDb": "Server=localhost;Database=DeadpoolDB;Integrated Security=true;TrustServerCertificate=true;"
  }
}
```

Edit `src/Deadpool.UI/appsettings.json` with the same connection string.

#### 4. Build Solution
```bash
dotnet restore
dotnet build
```

#### 5. Run Agent (Console Mode)
```bash
cd src\Deadpool.Agent
dotnet run
```

#### 6. Run UI Application
```bash
cd src\Deadpool.UI
dotnet run
```

### Installing Agent as Windows Service

```powershell
# Publish as self-contained executable
dotnet publish src\Deadpool.Agent\Deadpool.Agent.csproj -c Release -r win-x64 --self-contained

# Install as Windows service (requires Administrator)
sc.exe create "Deadpool Agent" binPath="C:\Path\To\Published\Deadpool.Agent.exe"
sc.exe description "Deadpool Agent" "Deadpool Backup Tools - SQL Server Backup Agent"
sc.exe start "Deadpool Agent"

# Verify service is running
sc.exe query "Deadpool Agent"
```

## 📖 Usage

### Add a SQL Server Instance

1. Open Deadpool UI
2. Navigate to **Servers → Manage Servers**
3. Click **Add Server**
4. Enter server name (e.g., `localhost` or `SERVERNAME\INSTANCENAME`)
5. Click **Test Connection** to verify
6. Click **Save**

### Create Backup Schedule

1. Navigate to **Backups → Manage Schedules**
2. Select a database
3. Click **Add Schedule**
4. Configure:
   - **Schedule Name** (e.g., "Daily Full Backup")
   - **Backup Type** (Full, Differential, or Log)
   - **Cron Expression** (e.g., `0 2 * * *` for 2 AM daily)
   - **Backup Path** (e.g., `D:\Backups\{database}_{type}_{date}.bak`)
   - **Retention** (days to keep backups)
5. Click **Save**

### Monitor Backup Jobs

1. Navigate to **Backups → Job Monitor**
2. View recent backup jobs with status
3. Filter by status, database, or date range
4. Click on a job to view details (timing, file size, errors)

## 🧪 Testing

### Run All Tests
```bash
dotnet test
```

### Run with Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Test Strategy
- **Unit Tests:** 90%+ coverage for Core (domain + application)
- **Integration Tests:** Repository and service implementations
- **End-to-End Tests:** Complete backup workflows

See [docs/TEST_STRATEGY.md](docs/TEST_STRATEGY.md) for comprehensive testing guidelines.

## 📚 Documentation

| Document | Description |
|----------|-------------|
| [PRODUCT.md](docs/PRODUCT.md) | Product vision, target market, roadmap, success metrics |
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | System architecture, layer responsibilities, technology choices |
| [DECISIONS.md](docs/DECISIONS.md) | Architectural Decision Records (ADRs) with rationale |
| [AGENTS.md](docs/AGENTS.md) | AI agent roles (Architect, Backend, UI, Testing, etc.) |
| [INSTRUCTIONS.md](docs/INSTRUCTIONS.md) | Coding standards, naming conventions, best practices |
| [DOMAIN.md](docs/DOMAIN.md) | Domain model, backup types, ubiquitous language |
| [TASKS.md](docs/TASKS.md) | Current sprint tasks, backlog, completed items |
| [TEST_STRATEGY.md](docs/TEST_STRATEGY.md) | Testing philosophy, tools, patterns, coverage goals |

## 🤝 Contributing

### Development Workflow

1. **Review Documentation** - Read [INSTRUCTIONS.md](docs/INSTRUCTIONS.md) for coding standards
2. **Pick a Task** - Choose from [TASKS.md](docs/TASKS.md) To Do list
3. **Create Feature Branch** - `git checkout -b feature/task-description`
4. **Write Tests First** - Follow TDD approach (see [TEST_STRATEGY.md](docs/TEST_STRATEGY.md))
5. **Implement Feature** - Follow coding standards
6. **Run Tests** - `dotnet test` (ensure all pass, 90%+ coverage)
7. **Update Documentation** - If behavior changes
8. **Commit Changes** - Descriptive commit message
9. **Create Pull Request** - For code review

### Code Standards

- **Naming:** PascalCase for classes/methods, camelCase for variables, `_underscore` for private fields
- **Async:** Always use `async`/`await`, never `.Result` or `.Wait()`
- **Null Safety:** Use nullable reference types (`#nullable enable`)
- **Error Handling:** Use `Result` pattern for business failures, exceptions for technical failures
- **Logging:** Structured logging with Serilog, use semantic logging (avoid string concatenation)

See [INSTRUCTIONS.md](docs/INSTRUCTIONS.md) for complete guidelines.

## 🛠️ Troubleshooting

### Agent Not Starting
- Check Event Viewer → Windows Logs → Application for errors
- Verify connection string in `appsettings.json`
- Ensure DeadpoolDB database exists with schema
- Check SQL Server service is running
- Review log files at `Deadpool.Agent\logs\`

### Backups Failing
- Check SQL Server account has `BACKUP DATABASE` permission
- Verify backup path is writable by SQL Server service account
- Ensure sufficient disk space
- Review job error message in UI or logs

### UI Cannot Connect
- Verify connection string in `Deadpool.UI\appsettings.json`
- Ensure SQL Server allows remote connections (if not localhost)
- Check firewall rules (SQL Server port 1433)

## 🎯 Roadmap

### Phase 1: Backup & Monitoring (Current)
- [x] Core domain entities
- [x] Infrastructure implementations
- [x] Worker service agents
- [x] Basic UI
- [ ] Complete repository implementations
- [ ] Add Cronos library for cron parsing
- [ ] Write comprehensive tests (90%+ coverage)

### Phase 2: One-Click Restore (Q3 2026)
- [ ] Backup chain validation
- [ ] Restore sequence calculation
- [ ] Point-in-time recovery calculator
- [ ] Restore preview UI
- [ ] Restore execution with progress tracking

### Phase 3: Auto Recovery (Q4 2026)
- [ ] Database health detection
- [ ] Automatic failure recovery
- [ ] Email and SMS notifications
- [ ] Cloud backup integration (Azure, AWS)

See [docs/TASKS.md](docs/TASKS.md) for detailed task tracking.

## 📄 License

**Proprietary Software**  
Copyright © 2026 Your Company Name. All rights reserved.

This software is proprietary and confidential. Unauthorized copying, distribution, or use of this software is strictly prohibited.

## 🏥 Why "Deadpool"?

Named after the Marvel character known for his regenerative healing abilities—just like this software helps databases recover and heal from disasters! 💪

---

## 📞 Support

- **Documentation:** See [docs/](docs/) folder
- **Issues:** [GitHub Issues](https://github.com/your-org/deadpool-backup-tools/issues)
- **Email:** support@yourcompany.com

## 🙏 Acknowledgments

- Built with ❤️ using .NET 8.0
- Inspired by hospital IT teams who need reliable backup solutions
- Architecture guided by Domain-Driven Design principles

---

**Status:** Phase 1 Development | **Latest Update:** 2026-04-28 | **Contributors:** [Your Name]
