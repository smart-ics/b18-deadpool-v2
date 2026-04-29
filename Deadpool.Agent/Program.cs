using Deadpool.Agent.Configuration;
using Deadpool.Agent.Workers;
using Deadpool.Core.Interfaces;
using Deadpool.Infrastructure.Scheduling;

var builder = Host.CreateApplicationBuilder(args);

// Backup-policy configuration
builder.Services.Configure<List<DatabaseBackupPolicyOptions>>(
    builder.Configuration.GetSection("BackupPolicies"));

// Schedule tracker (singleton — must survive across poll ticks)
builder.Services.AddSingleton<IScheduleTracker, InMemoryScheduleTracker>();

// Scheduler worker
builder.Services.AddHostedService<BackupSchedulerWorker>();

var host = builder.Build();
host.Run();
