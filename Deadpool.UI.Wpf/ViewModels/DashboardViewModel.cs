using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Deadpool.UI.Wpf.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private readonly IDashboardMonitoringService? _dashboardService;
    private readonly IAgentHeartbeatRepository? _heartbeatRepository;
    private readonly ILogger<DashboardViewModel>? _logger;
    private readonly string _databaseName;
    private readonly string _backupVolumePath;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string _headerPrimaryText = "CRITICAL - STORAGE FAILURE IMMINENT";
    private string _headerSecondaryText = "Last update: --:--";
    private Brush _headerStatusBrush = Brushes.White;

    private string _lastBackupStatusText = "UNKNOWN";
    private string _lastBackupDetailText = "FULL --   DIFF --   LOG --";
    private Brush _lastBackupStatusBrush = Brushes.White;

    private string _chainHealthStatusText = "UNKNOWN";
    private string _chainHealthDetailText = "Restore chain broken";
    private Brush _chainHealthStatusBrush = Brushes.White;

    private string _storageStatusText = "UNKNOWN";
    private string _storageDetailText = "Free: --\nNext: --\nExhaustion: --";
    private Brush _storageStatusBrush = Brushes.White;

    private string _databaseStatusText = "UNKNOWN";
    private string _databaseDetailText = "Instance: --";
    private Brush _databaseStatusBrush = Brushes.White;
    private string _agentStatus = "Offline";
    private string _lastSeen = "--";
    private string _agentStatusDisplay = "❌ Agent Offline (last seen --)";

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsLoaded { get; private set; }

    public string HeaderPrimaryText
    {
        get => _headerPrimaryText;
        private set => SetField(ref _headerPrimaryText, value);
    }

    public string HeaderSecondaryText
    {
        get => _headerSecondaryText;
        private set => SetField(ref _headerSecondaryText, value);
    }

    public Brush HeaderStatusBrush
    {
        get => _headerStatusBrush;
        private set => SetField(ref _headerStatusBrush, value);
    }

    public string LastBackupStatusText
    {
        get => _lastBackupStatusText;
        private set => SetField(ref _lastBackupStatusText, value);
    }

    public string LastBackupDetailText
    {
        get => _lastBackupDetailText;
        private set => SetField(ref _lastBackupDetailText, value);
    }

    public Brush LastBackupStatusBrush
    {
        get => _lastBackupStatusBrush;
        private set => SetField(ref _lastBackupStatusBrush, value);
    }

    public string ChainHealthStatusText
    {
        get => _chainHealthStatusText;
        private set => SetField(ref _chainHealthStatusText, value);
    }

    public string ChainHealthDetailText
    {
        get => _chainHealthDetailText;
        private set => SetField(ref _chainHealthDetailText, value);
    }

    public Brush ChainHealthStatusBrush
    {
        get => _chainHealthStatusBrush;
        private set => SetField(ref _chainHealthStatusBrush, value);
    }

    public string StorageStatusText
    {
        get => _storageStatusText;
        private set => SetField(ref _storageStatusText, value);
    }

    public string StorageDetailText
    {
        get => _storageDetailText;
        private set => SetField(ref _storageDetailText, value);
    }

    public Brush StorageStatusBrush
    {
        get => _storageStatusBrush;
        private set => SetField(ref _storageStatusBrush, value);
    }

    public string DatabaseStatusText
    {
        get => _databaseStatusText;
        private set => SetField(ref _databaseStatusText, value);
    }

    public string DatabaseDetailText
    {
        get => _databaseDetailText;
        private set => SetField(ref _databaseDetailText, value);
    }

    public Brush DatabaseStatusBrush
    {
        get => _databaseStatusBrush;
        private set => SetField(ref _databaseStatusBrush, value);
    }

    public string AgentStatus
    {
        get => _agentStatus;
        private set => SetField(ref _agentStatus, value);
    }

    public string LastSeen
    {
        get => _lastSeen;
        private set => SetField(ref _lastSeen, value);
    }

    public string AgentStatusDisplay
    {
        get => _agentStatusDisplay;
        private set => SetField(ref _agentStatusDisplay, value);
    }

    public ObservableCollection<JobRow> RecentJobs { get; } = new();
    public ObservableCollection<AlertItemRow> Alerts { get; } = new();

    public DashboardViewModel(
        IDashboardMonitoringService dashboardService,
        IAgentHeartbeatRepository heartbeatRepository,
        ILogger<DashboardViewModel> logger,
        string databaseName,
        string backupVolumePath)
    {
        _dashboardService = dashboardService;
        _heartbeatRepository = heartbeatRepository;
        _logger = logger;
        _databaseName = databaseName;
        _backupVolumePath = backupVolumePath;
    }

    private DashboardViewModel()
    {
        _databaseName = "DESIGN";
        _backupVolumePath = string.Empty;
    }

    public async Task LoadAsync()
    {
        if (_dashboardService == null || IsLoaded)
        {
            return;
        }

        await RefreshAsync();
        IsLoaded = true;
    }

    public async Task RefreshAsync()
    {
        if (_dashboardService == null)
        {
            return;
        }

        if (!await _refreshLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            var snapshot = await _dashboardService.GetDashboardSnapshotAsync(_databaseName, _backupVolumePath);
            var lastSeenUtc = _heartbeatRepository == null
                ? null
                : await _heartbeatRepository.GetLastSeenUtcAsync();

            MapSnapshot(snapshot);
            MapAgentHeartbeat(lastSeenUtc);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load dashboard snapshot for database '{DatabaseName}'.", _databaseName);
            HeaderPrimaryText = "WARNING - DASHBOARD DATA UNAVAILABLE";
            HeaderSecondaryText = $"Last update: {DateTime.Now:HH:mm}";
            HeaderStatusBrush = Brushes.Goldenrod;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private void MapSnapshot(DashboardSnapshot snapshot)
    {
        HeaderPrimaryText = BuildHeaderText(snapshot);
        HeaderSecondaryText = $"Last update: {DateTime.Now:HH:mm}";
        HeaderStatusBrush = ToBrush(snapshot.LastBackupStatus.OverallHealth);

        LastBackupStatusText = snapshot.LastBackupStatus.ChainHealthSummary.ToUpperInvariant();
        LastBackupDetailText =
            $"FULL {FormatTimestamp(snapshot.LastBackupStatus.LastFullBackup)}   " +
            $"DIFF {FormatTimestamp(snapshot.LastBackupStatus.LastDifferentialBackup)}   " +
            $"LOG {FormatTimestamp(snapshot.LastBackupStatus.LastLogBackup)}";
        LastBackupStatusBrush = ToBrush(snapshot.LastBackupStatus.OverallHealth);

        var chainHealth = snapshot.ChainInitializationStatus.RestoreChainHealth;
        ChainHealthStatusText = chainHealth.ToUpperInvariant();
        ChainHealthDetailText = string.IsNullOrWhiteSpace(snapshot.ChainInitializationStatus.WarningMessage)
            ? "Restore chain healthy"
            : "Restore chain broken";
        ChainHealthStatusBrush = chainHealth.Equals("Healthy", StringComparison.OrdinalIgnoreCase)
            ? Brushes.LimeGreen
            : Brushes.Red;

        var freeGb = BytesToGigabytes(snapshot.StorageStatus.FreeBytes);
        var totalGb = BytesToGigabytes(snapshot.StorageStatus.TotalBytes);
        StorageStatusText = snapshot.StorageStatus.OverallHealth switch
        {
            HealthStatus.Critical => "WILL FAIL",
            HealthStatus.Warning => "AT RISK",
            _ => "HEALTHY"
        };
        StorageDetailText =
            $"Free: {freeGb:F0}GB\n" +
            $"Next: --\n" +
            $"Exhaustion: {(snapshot.StorageStatus.OverallHealth == HealthStatus.Critical ? "~2h" : "--")}";
        StorageStatusBrush = ToBrush(snapshot.StorageStatus.OverallHealth);

        DatabaseStatusText = snapshot.DatabasePulseStatus?.Status.ToString().ToUpperInvariant() ?? "UNKNOWN";
        DatabaseDetailText = $"Instance: {snapshot.DatabaseName} | Free: {freeGb:F0}/{totalGb:F0}GB";
        DatabaseStatusBrush = DatabaseStatusText.Equals("ONLINE", StringComparison.OrdinalIgnoreCase)
            ? Brushes.LimeGreen
            : Brushes.Goldenrod;

        RecentJobs.Clear();
        foreach (var job in snapshot.RecentJobs.OrderByDescending(j => j.StartTime ?? DateTime.MinValue).Take(12))
        {
            RecentJobs.Add(new JobRow(
                job.BackupType.ToString().ToUpperInvariant(),
                (job.EndTime ?? job.StartTime)?.ToString("HH:mm") ?? "--:--",
                MapJobStatus(job.Status)));
        }

        Alerts.Clear();
        foreach (var issue in snapshot.LastBackupStatus.CriticalIssues)
        {
            Alerts.Add(new AlertItemRow($"❌ {NormalizeIssue(issue)}", "👉 INVESTIGATE CHAIN"));
        }

        foreach (var warning in snapshot.StorageStatus.Warnings.Take(2))
        {
            Alerts.Add(new AlertItemRow($"⚠ {NormalizeIssue(warning)}", "👉 FREE DISK SPACE"));
        }

        if (Alerts.Count == 0)
        {
            Alerts.Add(new AlertItemRow("✔ No active alerts", "👉 CONTINUE MONITORING"));
        }
    }

    public static DashboardViewModel CreateDesignSample()
    {
        var vm = new DashboardViewModel
        {
            HeaderPrimaryText = "CRITICAL - STORAGE FAILURE IMMINENT",
            HeaderSecondaryText = "Last update: 10:25",
            HeaderStatusBrush = Brushes.Red,
            LastBackupStatusText = "HEALTHY",
            LastBackupDetailText = "FULL ✔ 10:25   DIFF ✔ 10:26   LOG ✔ 10:26",
            LastBackupStatusBrush = Brushes.LimeGreen,
            ChainHealthStatusText = "CRITICAL",
            ChainHealthDetailText = "Restore chain broken",
            ChainHealthStatusBrush = Brushes.Red,
            StorageStatusText = "WILL FAIL",
            StorageDetailText = "Free: 15GB\nNext: 45GB ❌\nExhaustion: ~2h",
            StorageStatusBrush = Brushes.Goldenrod,
            DatabaseStatusText = "ONLINE",
            DatabaseDetailText = "Instance: PROD-SQL-01",
            DatabaseStatusBrush = Brushes.LimeGreen,
            IsLoaded = true
        };

        vm.RecentJobs.Add(new JobRow("FULL", "10:25", "SUCCESS"));
        vm.RecentJobs.Add(new JobRow("DIFF", "10:26", "SUCCESS"));
        vm.RecentJobs.Add(new JobRow("LOG", "10:26", "SUCCESS"));
        vm.RecentJobs.Add(new JobRow("LOG", "10:27", "FAILED"));
        vm.RecentJobs.Add(new JobRow("DIFF", "10:30", "PENDING"));

        vm.Alerts.Add(new AlertItemRow("❌ No FULL backup", "👉 RUN FULL NOW"));
        vm.Alerts.Add(new AlertItemRow("⚠ Storage pressure high", "👉 PURGE OLD CHAINS"));
        vm.Alerts.Add(new AlertItemRow("❌ Restore risk", "👉 VERIFY BACKUP ORDER"));

        return vm;
    }

    private static string BuildHeaderText(DashboardSnapshot snapshot)
    {
        return snapshot.LastBackupStatus.OverallHealth switch
        {
            HealthStatus.Critical => "CRITICAL - STORAGE FAILURE IMMINENT",
            HealthStatus.Warning => "WARNING - BACKUP HEALTH DEGRADED",
            _ => "HEALTHY - BACKUP OPERATIONS STABLE"
        };
    }

    private static string NormalizeIssue(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Unknown issue";
        }

        return text
            .Replace("Gap detected", "Restore chain broken", StringComparison.OrdinalIgnoreCase)
            .Replace("Estimated capacity", "Exhaustion", StringComparison.OrdinalIgnoreCase)
            .Replace("Log chain gap", "Restore risk", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatTimestamp(DateTime? value)
    {
        return value.HasValue ? $"✔ {value.Value:HH:mm}" : "--";
    }

    private static string MapJobStatus(BackupStatus status)
    {
        return status switch
        {
            BackupStatus.Completed => "SUCCESS",
            BackupStatus.Failed => "FAILED",
            BackupStatus.Pending => "PENDING",
            BackupStatus.Running => "PENDING",
            _ => "PENDING"
        };
    }

    private static Brush ToBrush(HealthStatus status)
    {
        return status switch
        {
            HealthStatus.Healthy => Brushes.LimeGreen,
            HealthStatus.Warning => Brushes.Goldenrod,
            HealthStatus.Critical => Brushes.Red,
            _ => Brushes.White
        };
    }

    private static decimal BytesToGigabytes(long bytes)
    {
        return bytes / 1024m / 1024m / 1024m;
    }

    private void MapAgentHeartbeat(DateTime? lastSeenUtc)
    {
        if (!lastSeenUtc.HasValue)
        {
            AgentStatus = "Offline";
            LastSeen = "--";
            AgentStatusDisplay = "❌ Agent Offline (last seen --)";
            return;
        }

        var age = DateTime.UtcNow - lastSeenUtc.Value;
        var minutesAgo = Math.Max(0, (int)Math.Floor(age.TotalMinutes));
        var isOnline = age <= TimeSpan.FromMinutes(2);

        AgentStatus = isOnline ? "Online" : "Offline";
        LastSeen = lastSeenUtc.Value.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        AgentStatusDisplay = isOnline
            ? "✔ Agent Online"
            : $"❌ Agent Offline (last seen {minutesAgo}m ago)";
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record JobRow(string Type, string Time, string Status);
public sealed record AlertItemRow(string Message, string Action);
