using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Deadpool.UI.Configuration;
using Microsoft.Extensions.Logging;

namespace Deadpool.UI;

/// <summary>
/// Operational dashboard showing backup health at a glance.
/// Read-only monitoring view for EDP operators.
/// </summary>
public partial class MonitoringDashboard : Form
{
    private readonly IDashboardMonitoringService _dashboardService;
    private readonly IBackupPolicyDisplayFormatter _policyDisplayFormatter;
    private readonly IDatabasePulseService _databasePulseService;
    private readonly ILogger<MonitoringDashboard> _logger;
    private readonly string _databaseName;
    private readonly string _databaseServerAddress;
    private readonly string _backupVolumePath;
    private readonly DatabaseBackupPolicyOptions? _backupPolicy;
    private readonly System.Windows.Forms.Timer? _autoRefreshTimer;
    private DateTime _lastUpdateTime;

    public MonitoringDashboard(
        IDashboardMonitoringService dashboardService,
        IBackupPolicyDisplayFormatter policyDisplayFormatter,
        IDatabasePulseService databasePulseService,
        ILogger<MonitoringDashboard> logger,
        string databaseName,
        string databaseServerAddress,
        string backupVolumePath,
        int autoRefreshIntervalSeconds = 60,
        DatabaseBackupPolicyOptions? backupPolicy = null)
    {
        _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
        _policyDisplayFormatter = policyDisplayFormatter ?? throw new ArgumentNullException(nameof(policyDisplayFormatter));
        _databasePulseService = databasePulseService ?? throw new ArgumentNullException(nameof(databasePulseService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _databaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
        _databaseServerAddress = databaseServerAddress ?? throw new ArgumentNullException(nameof(databaseServerAddress));
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
            await DisplayDatabasePulseAsync();

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
        // Show safe "Unknown" state rather than false Critical alarms
        lblFullBackup.Text = "Full: Unknown";
        lblDiffBackup.Text = "Differential: Unknown";
        lblLogBackup.Text = "Log: Unknown";
        lblChainHealth.Text = "Chain Health: Unknown";
        lblChainHealth.ForeColor = Color.Gray;

        lstWarnings.Items.Clear();
        lstWarnings.Items.Add("⚠️ Unable to load backup status");

        panelLastBackupStatus.BackColor = Color.White;

        dgvRecentJobs.DataSource = null;

        lblStoragePath.Text = "Path: Unknown";
        lblStorageSpace.Text = "Free: Unknown";
        lblStorageHealth.Text = "Health: Unknown";
        lblStorageHealth.ForeColor = Color.Gray;
        progressBarStorage.Value = 0;
        panelStorageStatus.BackColor = Color.White;

        lblPulseStatus.Text = "Status: Unknown";
        lblPulseStatus.ForeColor = Color.Gray;
        lblPulseLastChecked.Text = "Last Checked: --";
        lblChainInitialized.Text = "Backup Chain Initialized: Unknown";
        lblLastValidFullBackup.Text = "Last Valid Full Backup: Not available";
        lblRestoreChainHealthSimple.Text = "Restore Chain Health: Unknown";
        lblChainInitializationWarning.Text = string.Empty;
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
    }

    private void DisplayLastBackupStatus(LastBackupStatus status)
    {
        // Display backup times
        lblFullBackup.Text = $"Full: {FormatBackupTime(status.LastFullBackup)}";
        lblDiffBackup.Text = $"Differential: {FormatBackupTime(status.LastDifferentialBackup)}";
        lblLogBackup.Text = $"Log: {FormatBackupTime(status.LastLogBackup)}";

        // Display chain health
        lblChainHealth.Text = $"Chain Health: {status.ChainHealthSummary}";
        lblChainHealth.ForeColor = GetHealthColor(status.OverallHealth);

        // Display warnings and critical issues
        lstWarnings.Items.Clear();
        foreach (var critical in status.CriticalIssues)
        {
            lstWarnings.Items.Add($"❌ CRITICAL: {critical}");
        }
        foreach (var warning in status.Warnings)
        {
            lstWarnings.Items.Add($"⚠️ WARNING: {warning}");
        }

        if (status.CriticalIssues.Count == 0 && status.Warnings.Count == 0)
        {
            lstWarnings.Items.Add("✅ No warnings or issues");
        }

        // Set panel background color based on health
        panelLastBackupStatus.BackColor = GetHealthBackgroundColor(status.OverallHealth);
    }

    private void DisplayRecentJobs(List<RecentJobSummary> recentJobs)
    {
        dgvRecentJobs.DataSource = null;

        var displayJobs = recentJobs.Select(job => new
        {
            Type = job.BackupType.ToString(),
            StartTime = job.StartTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--",
            EndTime = job.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--",
            Status = job.Status.ToString(),
            Duration = job.Duration.HasValue ? $"{job.Duration.Value.TotalMinutes:F1}m" : "--",
            Error = job.ErrorMessage ?? ""
        }).ToList();

        dgvRecentJobs.DataSource = displayJobs;

        // Color code rows by status
        foreach (DataGridViewRow row in dgvRecentJobs.Rows)
        {
            var status = row.Cells["Status"].Value?.ToString();
            if (status == "Completed")
            {
                row.DefaultCellStyle.BackColor = Color.LightGreen;
            }
            else if (status == "Failed")
            {
                row.DefaultCellStyle.BackColor = Color.LightCoral;
            }
            else if (status == "InProgress" || status == "Running")
            {
                row.DefaultCellStyle.BackColor = Color.LightYellow;
            }
        }
    }

    private void DisplayStorageStatus(StorageStatusSummary storage)
    {
        lblStoragePath.Text = $"Path: {storage.VolumePath}";
        lblStorageSpace.Text = $"Free: {FormatBytes(storage.FreeBytes)} / {FormatBytes(storage.TotalBytes)} ({storage.FreePercentage:F1}%)";
        lblStorageHealth.Text = $"Health: {storage.OverallHealth}";
        lblStorageHealth.ForeColor = GetHealthColor(storage.OverallHealth);

        // Update progress bar (inverted - shows used space)
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

        panelStorageStatus.BackColor = GetHealthBackgroundColor(storage.OverallHealth);
    }

    private string FormatBackupTime(DateTime? backupTime)
    {
        if (!backupTime.HasValue)
            return "Never";

        var localTime = backupTime.Value.ToLocalTime();
        var age = DateTime.UtcNow - backupTime.Value;

        if (age.TotalHours < 1)
            return $"{localTime:HH:mm:ss} ({age.TotalMinutes:F0}m ago)";
        else if (age.TotalDays < 1)
            return $"{localTime:HH:mm:ss} ({age.TotalHours:F1}h ago)";
        else
            return $"{localTime:yyyy-MM-dd HH:mm} ({age.TotalDays:F1}d ago)";
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

    private void DisplayDatabaseTopology()
    {
        lblDbName.Text = $"Database: {_databaseName}";
        lblDbServer.Text = $"Production SQL Server: {_databaseServerAddress}";
        lblDbRecoveryModel.Text = $"Recovery Model: {(_backupPolicy?.RecoveryModel ?? "Unknown")}";

        var backupServer = ResolveBackupStorageServer(_backupVolumePath);
        lblTopologyProdServer.Text = $"Production DB Server: {_databaseServerAddress}";
        lblTopologyBackupServer.Text = $"Backup Storage Server: {backupServer}";
        lblTopologyDestinationPath.Text = $"Backup Destination: {_backupVolumePath}";
    }

    private async Task DisplayDatabasePulseAsync()
    {
        var result = await _databasePulseService.CheckAsync();
        lblPulseStatus.Text = $"Status: {result.Status}";
        lblPulseStatus.ForeColor = GetHealthColor(result.Status);
        lblPulseLastChecked.Text = $"Last Checked: {result.LastCheckedUtc:yyyy-MM-dd HH:mm:ss} UTC";

        if (result.Status == HealthStatus.Critical && !string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            _logger.LogError("Database connectivity critical. Error: {ErrorMessage}", result.ErrorMessage);
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
}
