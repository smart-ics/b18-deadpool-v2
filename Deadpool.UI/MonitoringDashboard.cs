using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Deadpool.UI.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;

namespace Deadpool.UI;

/// <summary>
/// Operational dashboard showing backup health at a glance.
/// Read-only monitoring view for EDP operators.
/// </summary>
public partial class MonitoringDashboard : Form
{
    private readonly IDashboardMonitoringService _dashboardService;
    private readonly IBackupPolicyDisplayFormatter _policyDisplayFormatter;
    private readonly ILogger<MonitoringDashboard> _logger;
    private readonly string _databaseName;
    private readonly string _backupVolumePath;
    private readonly DatabaseBackupPolicyOptions? _backupPolicy;
    private readonly System.Windows.Forms.Timer? _autoRefreshTimer;
    private DateTime _lastUpdateTime;
    private long? _estimatedNextFullBackupBytes;

    public MonitoringDashboard(
        IDashboardMonitoringService dashboardService,
        IBackupPolicyDisplayFormatter policyDisplayFormatter,
        ILogger<MonitoringDashboard> logger,
        string databaseName,
        string backupVolumePath,
        int autoRefreshIntervalSeconds = 60,
        DatabaseBackupPolicyOptions? backupPolicy = null)
    {
        _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
        _policyDisplayFormatter = policyDisplayFormatter ?? throw new ArgumentNullException(nameof(policyDisplayFormatter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _databaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
        _backupVolumePath = backupVolumePath ?? throw new ArgumentNullException(nameof(backupVolumePath));
        _backupPolicy = backupPolicy;

        InitializeComponent();
        DisplayBackupPolicySummary();
        DisplayDatabaseTopology();

        // Setup auto-refresh timer if enabled
        if (autoRefreshIntervalSeconds > 0)
        {
            _autoRefreshTimer = new System.Windows.Forms.Timer
            {
                Interval = autoRefreshIntervalSeconds * 1000
            };
            _autoRefreshTimer.Tick += async (s, e) => await RefreshDashboardAsync();
            _autoRefreshTimer.Start();
        }
    }

    private async void MonitoringDashboard_Load(object sender, EventArgs e)
    {
        await RefreshDashboardAsync();
    }

    private async void btnRefresh_Click(object sender, EventArgs e)
    {
        await RefreshDashboardAsync();
    }

    private void btnJobMonitor_Click(object sender, EventArgs e)
    {
        // Open job monitor form
        var serviceProvider = (Application.OpenForms[0] as MonitoringDashboard)?.Tag as IServiceProvider;
        if (serviceProvider == null)
        {
            MessageBox.Show("Cannot open job monitor: service provider not available.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var monitoringService = serviceProvider.GetService(typeof(IBackupJobMonitoringService)) as IBackupJobMonitoringService;
        if (monitoringService == null)
        {
            MessageBox.Show("Cannot open job monitor: monitoring service not available.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var jobMonitorForm = new BackupJobMonitorForm(monitoringService, _databaseName);
        jobMonitorForm.Show();
    }

    private async Task RefreshDashboardAsync()
    {
        try
        {
            btnRefresh.Enabled = false;
            lblLastRefresh.Text = "Refreshing...";

            var snapshot = await _dashboardService.GetDashboardSnapshotAsync(_databaseName, _backupVolumePath);

            DisplayLastBackupStatus(snapshot.LastBackupStatus);
            DisplayRecentJobs(snapshot.RecentJobs);
            DisplayStorageStatus(snapshot.StorageStatus);
            DisplayChainInitializationStatus(snapshot.ChainInitializationStatus);
            DisplayDatabasePulse(snapshot.DatabasePulseStatus);
            DisplayGlobalHeader(snapshot);
            DisplayActionableAlert(snapshot.LastBackupStatus, snapshot.StorageStatus, snapshot.ChainInitializationStatus);

            _lastUpdateTime = snapshot.SnapshotTime;
            lblLastRefresh.Text = $"Last refresh: {snapshot.SnapshotTime:yyyy-MM-dd HH:mm:ss} UTC";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to refresh dashboard: {ex.Message}\n\nPlease check that the backup database is accessible.",
                "Refresh Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            lblLastRefresh.Text = "Refresh failed - showing safe fallback state";
            DisplayFallbackState();
        }
        finally
        {
            btnRefresh.Enabled = true;
        }
    }

    private void DisplayFallbackState()
    {
        lblFullBackup.Text = "FULL  Unknown";
        lblDiffBackup.Text = "DIFF  Unknown";
        lblLogBackup.Text = "LOG   Unknown";
        lblChainHealth.Text = "UNKNOWN";
        lblChainHealth.ForeColor = Color.Gray;
        lblChainMessage.Text = "Unable to evaluate backup chain state";
        lblChainAction.Text = "Action: verify agent connectivity and refresh";

        lblAlertMessage.Text = "Unable to load backup status";
        lblAlertAction.Text = "-> Check SQL and storage connectivity";

        panelLastBackupStatus.BackColor = Color.White;
        panelChainHealthCard.BackColor = Color.White;

        dgvRecentJobs.DataSource = null;

        _estimatedNextFullBackupBytes = null;
        lblStoragePath.Text = "Volume: Unknown";
        lblStorageSpace.Text = "Free: Unknown";
        lblEstimatedFullBackup.Text = "Next FULL: Unknown";
        lblStoragePrediction.Text = "Status: CHECK";
        lblStoragePrediction.ForeColor = Color.Gray;
        progressBarStorage.Value = 0;
        panelStorageStatus.BackColor = Color.White;

        lblPulseStatus.Text = "Status: Unknown";
        lblPulseStatus.ForeColor = Color.Gray;
        lblPulseLastChecked.Text = "Last Checked: --";
        lblChainInitialized.Text = "Backup Chain Initialized: Unknown";
        lblLastValidFullBackup.Text = "Last Valid Full Backup: Not available";
        lblRestoreChainHealthSimple.Text = "Restore Chain Health: Unknown";
        lblChainInitializationWarning.Text = string.Empty;

        lblSystemStatus.Text = "UNKNOWN";
        lblSystemStatus.ForeColor = Color.Gray;
        lblDbSummary.Text = "Next Backup: FULL -- | DIFF -- | LOG --";
        lblNextRisk.Text = "Unable to determine next risk";
        lblDatabaseStatus.Text = "? Unknown";
        lblDatabaseStatus.ForeColor = Color.Gray;
    }

    private void DisplayBackupPolicySummary()
    {
        if (_backupPolicy == null)
        {
            lblPolicyFullSchedule.Text = "Full Backup: Policy not configured";
            lblPolicyDifferentialSchedule.Text = "Differential Backup: Policy not configured";
            lblPolicyLogSchedule.Text = "Transaction Log Backup: Policy not configured";
            lblPolicyRecoveryModel.Text = "Recovery Model: Unknown";
            lblPolicyRetention.Text = "Retention: Unknown";
            lblPolicyBootstrap.Text = string.Empty;
            lblDbSummary.Text = "Next Backup: FULL -- | DIFF -- | LOG --";
            return;
        }

        var summary = _policyDisplayFormatter.Format(
            _backupPolicy.FullBackupCron,
            _backupPolicy.DifferentialBackupCron,
            _backupPolicy.TransactionLogBackupCron,
            _backupPolicy.RecoveryModel,
            _backupPolicy.RetentionDays,
            _backupPolicy.BootstrapFullBackupEnabled);

        lblPolicyFullSchedule.Text = summary.FullBackupSchedule;
        lblPolicyDifferentialSchedule.Text = summary.DifferentialBackupSchedule;
        lblPolicyLogSchedule.Text = summary.TransactionLogBackupSchedule;
        lblPolicyRecoveryModel.Text = summary.RecoveryModel;
        lblPolicyRetention.Text = summary.Retention;
        lblPolicyBootstrap.Text = summary.BootstrapFullBackupEnabled ?? string.Empty;

        lblDbSummary.Text =
            $"Next Backup: FULL {ToCompactSchedule(summary.FullBackupSchedule)} | " +
            $"DIFF {ToCompactSchedule(summary.DifferentialBackupSchedule)} | " +
            $"LOG {ToCompactSchedule(summary.TransactionLogBackupSchedule)}";
    }

    private void DisplayLastBackupStatus(LastBackupStatus status)
    {
        lblFullBackup.Text = $"FULL {GetBackupStateIcon(status.LastFullBackup)} {FormatBackupTime(status.LastFullBackup)}";
        lblDiffBackup.Text = $"DIFF {GetBackupStateIcon(status.LastDifferentialBackup)} {FormatBackupTime(status.LastDifferentialBackup)}";
        lblLogBackup.Text = $"LOG  {GetBackupStateIcon(status.LastLogBackup)} {FormatBackupTime(status.LastLogBackup)}";

        lblFullBackup.ForeColor = status.LastFullBackup.HasValue ? Color.FromArgb(16, 124, 16) : Color.FromArgb(196, 43, 28);
        lblDiffBackup.ForeColor = status.LastDifferentialBackup.HasValue ? Color.FromArgb(16, 124, 16) : Color.FromArgb(196, 43, 28);
        lblLogBackup.ForeColor = status.LastLogBackup.HasValue ? Color.FromArgb(16, 124, 16) : Color.FromArgb(196, 43, 28);

        lblChainHealth.Text = status.OverallHealth.ToString().ToUpperInvariant();
        lblChainHealth.ForeColor = GetHealthColor(status.OverallHealth);
        panelChainHealthCard.BackColor = GetHealthBackgroundColor(status.OverallHealth);

        var chainMessage = status.CriticalIssues.FirstOrDefault()
            ?? status.Warnings.FirstOrDefault()
            ?? "Backup chain is healthy";
        lblChainMessage.Text = chainMessage;
        lblChainAction.Text = BuildChainActionSuggestion(status).Replace("Action: ", "-> ");

        panelLastBackupStatus.BackColor = GetHealthBackgroundColor(status.OverallHealth);
    }

    private void DisplayRecentJobs(List<RecentJobSummary> recentJobs)
    {
        dgvRecentJobs.DataSource = null;

        _estimatedNextFullBackupBytes = EstimateNextFullBackupSizeBytes(recentJobs);

        var displayJobs = recentJobs.Select(job => new
        {
            Type = GetBackupTypeLabel(job.BackupType),
            Time = (job.EndTime ?? job.StartTime)?.ToLocalTime().ToString("HH:mm") ?? "--",
            Status = GetJobStatusIcon(job.Status),
            RawStatus = job.Status.ToString()
        }).ToList();

        dgvRecentJobs.DataSource = displayJobs;

        if (dgvRecentJobs.Columns.Contains("RawStatus"))
        {
            dgvRecentJobs.Columns["RawStatus"].Visible = false;
        }

        if (dgvRecentJobs.Columns.Contains("Type"))
        {
            dgvRecentJobs.Columns["Type"].Width = 80;
        }

        if (dgvRecentJobs.Columns.Contains("Time"))
        {
            dgvRecentJobs.Columns["Time"].Width = 72;
        }

        if (dgvRecentJobs.Columns.Contains("Status"))
        {
            dgvRecentJobs.Columns["Status"].Width = 70;
        }

        foreach (DataGridViewRow row in dgvRecentJobs.Rows)
        {
            var status = row.Cells["RawStatus"].Value?.ToString();
            if (status == "Completed")
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(236, 247, 236);
            }
            else if (status == "Failed")
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(253, 237, 237);
            }
            else if (status == "InProgress" || status == "Running")
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 247, 224);
            }
        }

        dgvRecentJobs.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvRecentJobs.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        dgvRecentJobs.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
    }

    private void DisplayStorageStatus(StorageStatusSummary storage)
    {
        lblStoragePath.Text = $"Path: {storage.VolumePath}";
        lblStorageSpace.Text = $"Free: {FormatBytes(storage.FreeBytes)} ({storage.FreePercentage:F1}%)";

        var estimatedBytes = _estimatedNextFullBackupBytes;
        lblEstimatedFullBackup.Text = estimatedBytes.HasValue
            ? $"Next: {FormatBytes(estimatedBytes.Value)}"
            : "Next: Unknown";

        var prediction = GetStoragePrediction(storage, estimatedBytes);
        lblStoragePrediction.Text = prediction == "WILL FAIL" ? "FAIL" : prediction;
        lblStoragePrediction.ForeColor = prediction == "WILL FAIL"
            ? Color.FromArgb(196, 43, 28)
            : prediction == "OK"
                ? Color.FromArgb(16, 124, 16)
                : Color.Gray;

        var usedPercentage = 100 - (int)storage.FreePercentage;
        progressBarStorage.Value = Math.Min(100, Math.Max(0, usedPercentage));

        // Set progress bar color based on health
        if (storage.OverallHealth == HealthStatus.Critical)
        {
            progressBarStorage.ForeColor = Color.Red;
        }
        else if (storage.OverallHealth == HealthStatus.Warning)
        {
            progressBarStorage.ForeColor = Color.Orange;
        }
        else
        {
            progressBarStorage.ForeColor = Color.Green;
        }

        var storageHealth = prediction == "WILL FAIL" ? HealthStatus.Critical : storage.OverallHealth;
        panelStorageStatus.BackColor = GetHealthBackgroundColor(storageHealth);
    }

    private void DisplayGlobalHeader(DashboardSnapshot snapshot)
    {
        var systemHealth = GetOverallSystemHealth(snapshot);
        var systemIcon = systemHealth switch
        {
            HealthStatus.Critical => "❌",
            HealthStatus.Warning => "⚠",
            HealthStatus.Healthy => "✔",
            _ => "?"
        };
        lblSystemStatus.Text = $"{systemIcon} {systemHealth.ToString().ToUpperInvariant()}";
        lblSystemStatus.ForeColor = GetHealthColor(systemHealth);
        lblNextRisk.Text = BuildNextRiskMessage(snapshot);
    }

    private void DisplayActionableAlert(
        LastBackupStatus backupStatus,
        StorageStatusSummary storageStatus,
        ChainInitializationStatusSummary chainStatus)
    {
        var (message, action, health) = BuildActionableAlert(backupStatus, storageStatus, chainStatus, _estimatedNextFullBackupBytes);
        lblAlertMessage.Text = message;
        lblAlertAction.Text = $"-> {action}";
        panelAlert.BackColor = GetHealthBackgroundColor(health);
    }

    private string FormatBackupTime(DateTime? backupTime)
    {
        if (!backupTime.HasValue)
            return "--";

        var localTime = backupTime.Value.ToLocalTime();
        return localTime.ToString("HH:mm");
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private Color GetHealthColor(HealthStatus health)
    {
        return health switch
        {
            HealthStatus.Healthy => Color.Green,
            HealthStatus.Warning => Color.Orange,
            HealthStatus.Critical => Color.Red,
            _ => Color.Gray
        };
    }

    private Color GetHealthBackgroundColor(HealthStatus health)
    {
        return health switch
        {
            HealthStatus.Healthy => Color.FromArgb(240, 255, 240), // Light green
            HealthStatus.Warning => Color.FromArgb(255, 250, 220), // Light yellow
            HealthStatus.Critical => Color.FromArgb(255, 230, 230), // Light red
            _ => Color.White
        };
    }

    private static string GetHealthIcon(HealthStatus health)
    {
        return health switch
        {
            HealthStatus.Healthy => "OK",
            HealthStatus.Warning => "!",
            HealthStatus.Critical => "X",
            _ => "?"
        };
    }

    private static string GetBackupTypeLabel(BackupType backupType)
    {
        return backupType switch
        {
            BackupType.Full => "FULL",
            BackupType.Differential => "DIFF",
            BackupType.TransactionLog => "LOG",
            _ => backupType.ToString().ToUpperInvariant()
        };
    }

    private static string GetJobStatusIcon(BackupStatus status)
    {
        return status switch
        {
            BackupStatus.Completed => "✔",
            BackupStatus.Failed => "❌",
            BackupStatus.Running => "⚠",
            _ => "•"
        };
    }

    private static string BuildShortJobMessage(RecentJobSummary job)
    {
        if (job.Status == BackupStatus.Failed)
        {
            return string.IsNullOrWhiteSpace(job.ErrorMessage) ? "Backup failed" : "Backup failed - check job monitor";
        }

        if (job.Status == BackupStatus.Running)
        {
            return "Backup in progress";
        }

        return "Completed";
    }

    private static long? EstimateNextFullBackupSizeBytes(IEnumerable<RecentJobSummary> jobs)
    {
        var recentCompletedFull = jobs
            .Where(j => j.BackupType == BackupType.Full && j.Status == BackupStatus.Completed && !string.IsNullOrWhiteSpace(j.FilePath))
            .OrderByDescending(j => j.EndTime ?? j.StartTime)
            .FirstOrDefault();

        if (recentCompletedFull == null || string.IsNullOrWhiteSpace(recentCompletedFull.FilePath))
        {
            return null;
        }

        try
        {
            if (!File.Exists(recentCompletedFull.FilePath))
            {
                return null;
            }

            var fileInfo = new FileInfo(recentCompletedFull.FilePath);
            if (fileInfo.Length <= 0)
            {
                return null;
            }

            // Add a modest growth headroom for prediction.
            return (long)(fileInfo.Length * 1.15);
        }
        catch
        {
            return null;
        }
    }

    private string GetStoragePrediction(StorageStatusSummary storage, long? estimatedNextFullBackupBytes)
    {
        if (estimatedNextFullBackupBytes.HasValue)
        {
            return storage.FreeBytes >= estimatedNextFullBackupBytes.Value ? "OK" : "WILL FAIL";
        }

        return storage.OverallHealth switch
        {
            HealthStatus.Critical => "WILL FAIL",
            HealthStatus.Healthy => "OK",
            _ => "CHECK"
        };
    }

    private static HealthStatus MaxHealth(HealthStatus left, HealthStatus right)
    {
        return (HealthStatus)Math.Max((int)left, (int)right);
    }

    private static HealthStatus GetOverallSystemHealth(DashboardSnapshot snapshot)
    {
        var health = MaxHealth(snapshot.LastBackupStatus.OverallHealth, snapshot.StorageStatus.OverallHealth);
        if (snapshot.DatabasePulseStatus != null)
        {
            health = MaxHealth(health, snapshot.DatabasePulseStatus.Status);
        }

        if (snapshot.ChainInitializationStatus.IsInitialized == false)
        {
            health = MaxHealth(health, HealthStatus.Warning);
        }

        return health;
    }

    private string BuildNextRiskMessage(DashboardSnapshot snapshot)
    {
        var prediction = GetStoragePrediction(snapshot.StorageStatus, _estimatedNextFullBackupBytes);
        if (prediction == "WILL FAIL")
        {
            return "Storage insufficient for next FULL backup";
        }

        var criticalIssue = snapshot.LastBackupStatus.CriticalIssues.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(criticalIssue))
        {
            return criticalIssue;
        }

        var warning = snapshot.LastBackupStatus.Warnings.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(warning))
        {
            return warning;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.ChainInitializationStatus.WarningMessage))
        {
            return snapshot.ChainInitializationStatus.WarningMessage;
        }

        return "No immediate risk detected";
    }

    private static string BuildChainActionSuggestion(LastBackupStatus status)
    {
        if (!status.LastFullBackup.HasValue)
        {
            return "Action: run FULL backup now";
        }

        var critical = string.Join(' ', status.CriticalIssues).ToLowerInvariant();
        if (critical.Contains("full"))
        {
            return "Action: run FULL backup immediately";
        }

        if (critical.Contains("log") || string.Join(' ', status.Warnings).ToLowerInvariant().Contains("log"))
        {
            return "Action: verify LOG schedule and SQL Agent jobs";
        }

        return status.OverallHealth switch
        {
            HealthStatus.Critical => "Action: intervene now and run validation backup",
            HealthStatus.Warning => "Action: investigate warnings and verify next backup window",
            _ => "Action: continue monitoring"
        };
    }

    private (string Message, string Action, HealthStatus Health) BuildActionableAlert(
        LastBackupStatus backupStatus,
        StorageStatusSummary storageStatus,
        ChainInitializationStatusSummary chainStatus,
        long? estimatedNextFullBackupBytes)
    {
        if (!backupStatus.LastFullBackup.HasValue)
        {
            return (
                "No full backup found",
                "run FULL backup immediately",
                HealthStatus.Critical);
        }

        var storagePrediction = GetStoragePrediction(storageStatus, estimatedNextFullBackupBytes);
        if (storagePrediction == "WILL FAIL")
        {
            return (
                "Storage insufficient for next FULL backup",
                "free space or extend backup volume now",
                HealthStatus.Critical);
        }

        var criticalIssue = backupStatus.CriticalIssues.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(criticalIssue))
        {
            return (
                criticalIssue,
                "resolve failed/overdue backup job immediately",
                HealthStatus.Critical);
        }

        if (chainStatus.IsInitialized == false)
        {
            return (
                "Backup chain is not initialized",
                "run an initial FULL backup",
                HealthStatus.Warning);
        }

        var warning = backupStatus.Warnings.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(warning))
        {
            return (
                warning,
                "inspect schedule and job history",
                HealthStatus.Warning);
        }

        return (
            "All monitored backup signals are healthy",
            "no immediate action required",
            HealthStatus.Healthy);
    }

    private void DisplayDatabaseTopology()
    {
        lblDbName.Text = $"Database: {_databaseName}";
        lblDbServer.Text = "Production SQL Server: (see Agent configuration)";
        lblDbRecoveryModel.Text = $"Recovery Model: {(_backupPolicy?.RecoveryModel ?? "Unknown")}";

        var backupServer = ResolveBackupStorageServer(_backupVolumePath);
        lblTopologyProdServer.Text = "Production DB Server: (see Agent configuration)";
        lblTopologyBackupServer.Text = $"Backup Storage Server: {backupServer}";
        lblTopologyDestinationPath.Text = $"Backup Destination: {_backupVolumePath}";
    }

    private void DisplayDatabasePulse(DatabasePulseStatus? pulse)
    {
        if (pulse == null)
        {
            lblPulseStatus.Text = "Status: Unknown";
            lblPulseStatus.ForeColor = Color.Gray;
            lblPulseLastChecked.Text = "Last Checked: --";
            lblDatabaseStatus.Text = "? Unknown";
            lblDatabaseStatus.ForeColor = Color.Gray;
            return;
        }

        lblPulseStatus.Text = $"Status: {pulse.Status}";
        lblPulseStatus.ForeColor = GetHealthColor(pulse.Status);
        lblPulseLastChecked.Text = $"Last Checked: {pulse.LastCheckedUtc:yyyy-MM-dd HH:mm:ss} UTC";
        lblDatabaseStatus.Text = pulse.Status == HealthStatus.Critical ? "❌ Down" : "✔ Online";
        lblDatabaseStatus.ForeColor = GetHealthColor(pulse.Status);

        if (pulse.Status == HealthStatus.Critical && !string.IsNullOrWhiteSpace(pulse.ErrorMessage))
        {
            _logger.LogError("Database connectivity critical. Error: {ErrorMessage}", pulse.ErrorMessage);
        }
    }

    private void DisplayChainInitializationStatus(ChainInitializationStatusSummary status)
    {
        var initializedText = status.IsInitialized.HasValue
            ? (status.IsInitialized.Value ? "Yes" : "No")
            : "Unknown";
        lblChainInitialized.Text = $"Backup Chain Initialized: {initializedText}";

        if (status.LastValidFullBackupTime.HasValue)
        {
            var timestamp = status.LastValidFullBackupTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            var path = string.IsNullOrWhiteSpace(status.LastValidFullBackupPath)
                ? string.Empty
                : $" ({status.LastValidFullBackupPath})";
            lblLastValidFullBackup.Text = $"Last Valid Full Backup: {timestamp}{path}";
        }
        else
        {
            lblLastValidFullBackup.Text = "Last Valid Full Backup: Not available";
        }

        lblRestoreChainHealthSimple.Text = $"Restore Chain Health: {status.RestoreChainHealth}";
        lblRestoreChainHealthSimple.ForeColor = status.RestoreChainHealth switch
        {
            "Healthy" => Color.Green,
            "Unhealthy" => Color.Red,
            _ => Color.Gray
        };

        lblChainInitializationWarning.Text = status.WarningMessage;
        lblChainInitializationWarning.ForeColor = string.IsNullOrWhiteSpace(status.WarningMessage) ? Color.Gray : Color.Red;
    }

    private static string ResolveBackupStorageServer(string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            return "Unknown";

        if (destinationPath.StartsWith("\\\\", StringComparison.Ordinal))
        {
            var parts = destinationPath.Trim('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
                return parts[0];
        }

        return "Local or direct-attached storage";
    }

    private static string GetBackupStateIcon(DateTime? backupTime)
    {
        return backupTime.HasValue ? "✔" : "❌";
    }

    private static string ToCompactSchedule(string scheduleText)
    {
        if (string.IsNullOrWhiteSpace(scheduleText))
        {
            return "--";
        }

        var separatorIndex = scheduleText.IndexOf(':');
        var compact = separatorIndex >= 0
            ? scheduleText[(separatorIndex + 1)..]
            : scheduleText;

        compact = compact.Trim();
        return compact.Length <= 18 ? compact : compact[..18];
    }
}
