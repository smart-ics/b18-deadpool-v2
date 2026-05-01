using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Deadpool.UI.Configuration;
using Microsoft.Extensions.Logging;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;

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
    private List<RecentJobSummary> _timelineJobs = new();

    private const int CardPadding = 12;
    private readonly Color _bgColor = Color.FromArgb(243, 246, 251);
    private readonly Color _headerColor = Color.FromArgb(22, 34, 51);
    private readonly Color _cardColor = Color.White;

    private Panel? _panelBody;
    private TableLayoutPanel? _layoutBody;
    private FlowLayoutPanel? _leftCards;
    private Panel? _panelChainHealthCard;
    private Panel? _panelTimeline;
    private Panel? _panelAlert;

    private Label? _lblGlobalStatus;
    private Label? _lblDatabaseSummary;
    private Label? _lblNextRisk;
    private Label? _lblChainCardStatus;
    private Label? _lblChainCardMessage;
    private Label? _lblChainCardAction;
    private Label? _lblStorageRiskPrediction;
    private Label? _lblStorageRiskEstimate;
    private Label? _lblStorageRiskFree;
    private Label? _lblAlertMessage;
    private Label? _lblAlertAction;
    private Label? _lblTimelineLegend;

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
        BuildOperationalLayout();
        ConfigureRecentJobsGrid();
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
            DisplayActionableAlert(snapshot);

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

        if (_lblGlobalStatus != null)
        {
            _lblGlobalStatus.Text = "UNKNOWN";
            _lblGlobalStatus.BackColor = Color.FromArgb(110, 118, 129);
        }

        if (_lblDatabaseSummary != null)
            _lblDatabaseSummary.Text = "Databases: 1 total | 0 healthy | 1 warning | 0 critical";

        if (_lblNextRisk != null)
            _lblNextRisk.Text = "Next Risk: Telemetry unavailable";

        if (_lblAlertMessage != null)
            _lblAlertMessage.Text = "Unable to load backup status";

        if (_lblAlertAction != null)
            _lblAlertAction.Text = "Action: Verify connectivity to monitoring repositories and refresh.";

        _panelTimeline?.Invalidate();
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
        lblFullBackup.Text = $"FULL   {FormatBackupTime(status.LastFullBackup)}";
        lblDiffBackup.Text = $"DIFF   {FormatBackupTime(status.LastDifferentialBackup)}";
        lblLogBackup.Text = $"LOG    {FormatBackupTime(status.LastLogBackup)}";
        lblFullBackup.ForeColor = status.LastFullBackup.HasValue ? Color.FromArgb(28, 106, 49) : Color.FromArgb(183, 28, 28);
        lblDiffBackup.ForeColor = status.LastDifferentialBackup.HasValue ? Color.FromArgb(28, 106, 49) : Color.FromArgb(183, 28, 28);
        lblLogBackup.ForeColor = status.LastLogBackup.HasValue ? Color.FromArgb(28, 106, 49) : Color.FromArgb(183, 28, 28);

        // Display chain health
        lblChainHealth.Text = $"Chain Health: {status.ChainHealthSummary}";
        lblChainHealth.ForeColor = GetHealthColor(status.OverallHealth);

        if (_lblChainCardStatus != null)
            _lblChainCardStatus.Text = StatusBadgeText(status.OverallHealth);

        if (_lblChainCardMessage != null)
            _lblChainCardMessage.Text = BuildChainMessage(status);

        if (_lblChainCardAction != null)
            _lblChainCardAction.Text = BuildChainAction(status);

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
        _panelChainHealthCard!.BackColor = GetHealthBackgroundColor(status.OverallHealth);
    }

    private void DisplayRecentJobs(List<RecentJobSummary> recentJobs)
    {
        dgvRecentJobs.DataSource = null;

        _timelineJobs = recentJobs
            .Where(job => job.StartTime.HasValue)
            .OrderBy(job => job.StartTime)
            .ToList();

        var displayJobs = recentJobs.Select(job => new
        {
            StatusIcon = GetStatusIcon(job.Status),
            Type = job.BackupType.ToString(),
            StartTime = job.StartTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--",
            EndTime = job.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--",
            Status = job.Status.ToString(),
            Duration = job.Duration.HasValue ? $"{job.Duration.Value.TotalMinutes:F1}m" : "--",
            Message = BuildJobMessage(job)
        }).ToList();

        dgvRecentJobs.DataSource = displayJobs;

        if (dgvRecentJobs.Columns.Contains("EndTime"))
            dgvRecentJobs.Columns["EndTime"].Visible = false;

        if (dgvRecentJobs.Columns.Contains("StatusIcon"))
        {
            dgvRecentJobs.Columns["StatusIcon"].HeaderText = string.Empty;
            dgvRecentJobs.Columns["StatusIcon"].Width = 36;
        }

        if (dgvRecentJobs.Columns.Contains("Message"))
            dgvRecentJobs.Columns["Message"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        // Color code rows by status
        foreach (DataGridViewRow row in dgvRecentJobs.Rows)
        {
            var status = row.Cells["Status"].Value?.ToString();
            if (status == "Completed")
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(233, 247, 237);
            }
            else if (status == "Failed")
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 232, 232);
            }
            else if (status == "Running")
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 248, 220);
            }

            row.Height = 30;
        }

        _panelTimeline?.Invalidate();
    }

    private void DisplayStorageStatus(StorageStatusSummary storage)
    {
        lblStoragePath.Text = $"Path: {storage.VolumePath}";
        lblStorageSpace.Text = $"Free: {FormatBytes(storage.FreeBytes)} / {FormatBytes(storage.TotalBytes)} ({storage.FreePercentage:F1}%)";
        lblStorageHealth.Text = $"Health: {storage.OverallHealth}";
        lblStorageHealth.ForeColor = GetHealthColor(storage.OverallHealth);

        var estimate = TryExtractEstimatedNextFullBytes(storage);
        var willFail = WillLikelyFailNextFull(storage, estimate);

        if (_lblStorageRiskFree != null)
            _lblStorageRiskFree.Text = $"Free: {FormatBytes(storage.FreeBytes)}";

        if (_lblStorageRiskEstimate != null)
            _lblStorageRiskEstimate.Text = estimate.HasValue
                ? $"Next FULL est: {FormatBytes(estimate.Value)}"
                : "Next FULL est: -- (not provided)";

        if (_lblStorageRiskPrediction != null)
        {
            _lblStorageRiskPrediction.Text = willFail ? "Status: WILL FAIL" : "Status: OK";
            _lblStorageRiskPrediction.ForeColor = willFail ? Color.FromArgb(183, 28, 28) : Color.FromArgb(28, 106, 49);
        }

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
        lblDbServer.Text = "Production SQL Server: (see Agent configuration)";
        lblDbRecoveryModel.Text = $"Recovery Model: {(_backupPolicy?.RecoveryModel ?? "Unknown")}";

        var backupServer = ResolveBackupStorageServer(_backupVolumePath);
        lblTopologyProdServer.Text = "Production DB Server: (see Agent configuration)";
        lblTopologyBackupServer.Text = $"Backup Storage Server: {backupServer}";
        lblTopologyDestinationPath.Text = $"Backup Destination: {_backupVolumePath}";

        // Keep topology data available for operators while keeping the main dashboard focused.
        panelBackupPolicy.Visible = false;
    }

    private void DisplayDatabasePulse(DatabasePulseStatus? pulse)
    {
        if (pulse == null)
        {
            lblPulseStatus.Text = "Status: Unknown";
            lblPulseStatus.ForeColor = Color.Gray;
            lblPulseLastChecked.Text = "Last Checked: --";
            return;
        }

        lblPulseStatus.Text = $"Status: {pulse.Status}";
        lblPulseStatus.ForeColor = GetHealthColor(pulse.Status);
        lblPulseLastChecked.Text = $"Last Checked: {pulse.LastCheckedUtc:yyyy-MM-dd HH:mm:ss} UTC";

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

        if (!string.IsNullOrWhiteSpace(status.WarningMessage) && _lblChainCardMessage != null)
            _lblChainCardMessage.Text = status.WarningMessage;

        if (status.IsInitialized == false && _lblChainCardAction != null)
            _lblChainCardAction.Text = "Action: Run FULL backup to initialize chain.";
    }

    private void BuildOperationalLayout()
    {
        BackColor = _bgColor;
        panelHeader.BackColor = _headerColor;
        panelHeader.Height = 90;

        lblTitle.Text = "Operational Backup Dashboard";
        lblTitle.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
        lblTitle.Location = new Point(16, 8);

        _lblGlobalStatus = new Label
        {
            AutoSize = false,
            Name = "lblGlobalStatus",
            Text = "UNKNOWN",
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(110, 118, 129),
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Location = new Point(20, 46),
            Size = new Size(120, 30)
        };

        _lblDatabaseSummary = new Label
        {
            AutoSize = false,
            Name = "lblDatabaseSummary",
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            Location = new Point(152, 50),
            Size = new Size(300, 24),
            Text = "Databases: 1 total | 0 healthy | 0 warning | 0 critical"
        };

        _lblNextRisk = new Label
        {
            AutoSize = false,
            Name = "lblNextRisk",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Location = new Point(468, 50),
            Size = new Size(470, 24),
            Text = "Next Risk: --"
        };

        lblLastRefresh.Location = new Point(820, 14);
        lblLastRefresh.Size = new Size(260, 22);

        btnJobMonitor.Location = new Point(952, 46);
        btnJobMonitor.Size = new Size(110, 30);
        btnRefresh.Location = new Point(1070, 46);
        btnRefresh.Size = new Size(110, 30);

        panelHeader.Controls.Add(_lblGlobalStatus);
        panelHeader.Controls.Add(_lblDatabaseSummary);
        panelHeader.Controls.Add(_lblNextRisk);

        _panelBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            BackColor = _bgColor
        };

        _layoutBody = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        _layoutBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
        _layoutBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
        _layoutBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
        _layoutBody.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _leftCards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        ConfigureCard(panelLastBackupStatus, "Last Backup Status", 220);
        ConfigureLastBackupCard();

        _panelChainHealthCard = CreateCardPanel("Backup Chain Health", 150);
        _lblChainCardStatus = CreateStatusLineLabel("Status: --", 36, 26, true);
        _lblChainCardMessage = CreateStatusLineLabel("No chain message", 36, 62, false);
        _lblChainCardAction = CreateStatusLineLabel("Action: --", 36, 92, false);
        _panelChainHealthCard.Controls.Add(_lblChainCardStatus);
        _panelChainHealthCard.Controls.Add(_lblChainCardMessage);
        _panelChainHealthCard.Controls.Add(_lblChainCardAction);

        ConfigureCard(panelStorageStatus, "Storage Risk", 180);
        ConfigureStorageCard();

        _panelAlert = CreateCardPanel("Actionable Alert", 125);
        _lblAlertMessage = CreateStatusLineLabel("No active alert", 36, 38, true);
        _lblAlertAction = CreateStatusLineLabel("Action: Monitor normal operation.", 36, 73, false);
        _panelAlert.Controls.Add(_lblAlertMessage);
        _panelAlert.Controls.Add(_lblAlertAction);

        _leftCards.Controls.Add(panelLastBackupStatus);
        _leftCards.Controls.Add(_panelChainHealthCard);
        _leftCards.Controls.Add(panelStorageStatus);
        _leftCards.Controls.Add(_panelAlert);

        var centerColumn = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(12, 0, 12, 0)
        };
        centerColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 62F));
        centerColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 38F));

        _panelTimeline = CreateCardPanel("Backup Timeline", 380);
        _panelTimeline.Dock = DockStyle.Fill;
        _panelTimeline.Paint += panelTimeline_Paint;
        _lblTimelineLegend = new Label
        {
            AutoSize = false,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.DimGray,
            Location = new Point(20, 38),
            Size = new Size(500, 24),
            Text = "FULL (Blue)   DIFF (Teal)   LOG (Orange)   Failed jobs are marked in red"
        };
        _panelTimeline.Controls.Add(_lblTimelineLegend);

        ConfigureCard(panelDatabaseTopology, "Environment Overview", 220);
        ConfigureTopologyCard();

        centerColumn.Controls.Add(_panelTimeline, 0, 0);
        centerColumn.Controls.Add(panelDatabaseTopology, 0, 1);

        ConfigureCard(panelRecentJobs, "Recent Jobs", 560);
        panelRecentJobs.Dock = DockStyle.Fill;
        panelRecentJobs.Margin = new Padding(0);
        lblRecentJobsTitle.Visible = false;
        dgvRecentJobs.Location = new Point(16, 44);
        dgvRecentJobs.Size = new Size(panelRecentJobs.Width - 32, panelRecentJobs.Height - 60);
        dgvRecentJobs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

        _layoutBody.Controls.Add(_leftCards, 0, 0);
        _layoutBody.Controls.Add(centerColumn, 1, 0);
        _layoutBody.Controls.Add(panelRecentJobs, 2, 0);
        _panelBody.Controls.Add(_layoutBody);

        Controls.Remove(panelLastBackupStatus);
        Controls.Remove(panelStorageStatus);
        Controls.Remove(panelDatabaseTopology);
        Controls.Remove(panelRecentJobs);
        Controls.Remove(panelBackupPolicy);

        Controls.Add(panelBackupPolicy);
        Controls.Add(_panelBody);

        panelBackupPolicy.Visible = false;
    }

    private void ConfigureCard(Panel panel, string title, int height)
    {
        panel.Dock = DockStyle.Top;
        panel.Height = height;
        panel.BackColor = _cardColor;
        panel.Padding = new Padding(CardPadding);
        panel.Margin = new Padding(0, 0, 0, 12);
        panel.BorderStyle = BorderStyle.FixedSingle;

        var titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 41, 55),
            Location = new Point(12, 10),
            BackColor = Color.Transparent
        };

        panel.Controls.Add(titleLabel);
        titleLabel.BringToFront();
    }

    private Panel CreateCardPanel(string title, int height)
    {
        var panel = new Panel
        {
            Height = height,
            Dock = DockStyle.Top,
            BackColor = _cardColor,
            Padding = new Padding(CardPadding),
            Margin = new Padding(0, 0, 0, 12),
            BorderStyle = BorderStyle.FixedSingle
        };

        var titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 41, 55),
            Location = new Point(12, 10)
        };
        panel.Controls.Add(titleLabel);
        return panel;
    }

    private Label CreateStatusLineLabel(string text, int x, int y, bool emphasize)
    {
        return new Label
        {
            AutoSize = false,
            Location = new Point(x, y),
            Size = new Size(500, emphasize ? 26 : 22),
            Font = emphasize ? new Font("Segoe UI", 10F, FontStyle.Bold) : new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(17, 24, 39),
            Text = text
        };
    }

    private void ConfigureLastBackupCard()
    {
        lblLastBackupTitle.Visible = false;
        lstWarnings.Visible = false;

        lblFullBackup.Location = new Point(16, 44);
        lblDiffBackup.Location = new Point(16, 78);
        lblLogBackup.Location = new Point(16, 112);
        lblChainHealth.Location = new Point(16, 150);

        lblFullBackup.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        lblDiffBackup.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        lblLogBackup.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        lblChainHealth.Font = new Font("Segoe UI", 10F, FontStyle.Bold);

        lblFullBackup.AutoSize = true;
        lblDiffBackup.AutoSize = true;
        lblLogBackup.AutoSize = true;
        lblChainHealth.AutoSize = true;
    }

    private void ConfigureStorageCard()
    {
        lblStorageTitle.Visible = false;
        lblStoragePath.Visible = false;
        progressBarStorage.Visible = false;

        lblStorageSpace.Location = new Point(16, 44);
        lblStorageHealth.Location = new Point(16, 74);
        lblStorageSpace.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        lblStorageHealth.Font = new Font("Segoe UI", 10F, FontStyle.Bold);

        _lblStorageRiskFree = CreateStatusLineLabel("Free: --", 16, 104, false);
        _lblStorageRiskEstimate = CreateStatusLineLabel("Next FULL est: --", 16, 124, false);
        _lblStorageRiskPrediction = CreateStatusLineLabel("Status: --", 16, 146, true);

        panelStorageStatus.Controls.Add(_lblStorageRiskFree);
        panelStorageStatus.Controls.Add(_lblStorageRiskEstimate);
        panelStorageStatus.Controls.Add(_lblStorageRiskPrediction);
    }

    private void ConfigureTopologyCard()
    {
        lblDatabaseTopologyTitle.Visible = false;

        var labels = new[]
        {
            lblDbName,
            lblDbServer,
            lblDbRecoveryModel,
            lblTopologyProdServer,
            lblTopologyBackupServer,
            lblTopologyDestinationPath,
            lblPulseStatus,
            lblPulseLastChecked,
            lblChainInitialized,
            lblLastValidFullBackup,
            lblRestoreChainHealthSimple,
            lblChainInitializationWarning
        };

        var y = 42;
        foreach (var label in labels)
        {
            label.Location = new Point(16, y);
            label.Width = panelDatabaseTopology.Width - 32;
            y += label == lblLastValidFullBackup || label == lblChainInitializationWarning ? 34 : 20;
        }

        lblPulseStatus.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        lblChainInitialized.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        lblRestoreChainHealthSimple.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
    }

    private void ConfigureRecentJobsGrid()
    {
        dgvRecentJobs.BorderStyle = BorderStyle.None;
        dgvRecentJobs.BackgroundColor = Color.White;
        dgvRecentJobs.EnableHeadersVisualStyles = false;
        dgvRecentJobs.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        dgvRecentJobs.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 236, 245);
        dgvRecentJobs.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(31, 41, 55);
        dgvRecentJobs.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        dgvRecentJobs.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
        dgvRecentJobs.DefaultCellStyle.SelectionBackColor = Color.FromArgb(212, 227, 248);
        dgvRecentJobs.DefaultCellStyle.SelectionForeColor = Color.FromArgb(17, 24, 39);
        dgvRecentJobs.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        dgvRecentJobs.GridColor = Color.FromArgb(235, 239, 244);
        dgvRecentJobs.RowTemplate.Height = 30;
    }

    private void DisplayGlobalHeader(DashboardSnapshot snapshot)
    {
        var chainHealth = snapshot.ChainInitializationStatus.RestoreChainHealth.Equals("Unhealthy", StringComparison.OrdinalIgnoreCase)
            ? HealthStatus.Critical
            : snapshot.LastBackupStatus.OverallHealth;

        var pulseHealth = snapshot.DatabasePulseStatus?.Status ?? HealthStatus.Warning;
        var overall = GetWorstHealth(snapshot.LastBackupStatus.OverallHealth, snapshot.StorageStatus.OverallHealth, pulseHealth, chainHealth);

        if (_lblGlobalStatus != null)
        {
            _lblGlobalStatus.Text = StatusBadgeText(overall);
            _lblGlobalStatus.BackColor = GetHealthColor(overall);
        }

        if (_lblDatabaseSummary != null)
        {
            var healthy = overall == HealthStatus.Healthy ? 1 : 0;
            var warning = overall == HealthStatus.Warning ? 1 : 0;
            var critical = overall == HealthStatus.Critical ? 1 : 0;
            _lblDatabaseSummary.Text = $"Databases: 1 total | {healthy} healthy | {warning} warning | {critical} critical";
        }

        if (_lblNextRisk != null)
            _lblNextRisk.Text = $"Next Risk: {BuildNextRisk(snapshot)}";
    }

    private void DisplayActionableAlert(DashboardSnapshot snapshot)
    {
        if (_lblAlertMessage == null || _lblAlertAction == null || _panelAlert == null)
            return;

        var alert = BuildTopAlert(snapshot);
        _lblAlertMessage.Text = alert.message;
        _lblAlertAction.Text = $"Action: {alert.action}";
        _lblAlertMessage.ForeColor = GetHealthColor(alert.health);
        _panelAlert.BackColor = GetHealthBackgroundColor(alert.health);
    }

    private (HealthStatus health, string message, string action) BuildTopAlert(DashboardSnapshot snapshot)
    {
        if (snapshot.LastBackupStatus.CriticalIssues.Count > 0)
        {
            return (HealthStatus.Critical, snapshot.LastBackupStatus.CriticalIssues[0], "Run FULL backup immediately.");
        }

        if (!snapshot.ChainInitializationStatus.IsInitialized.GetValueOrDefault(true))
        {
            return (HealthStatus.Critical, "Backup chain is not initialized.", "Run FULL backup now to initialize restore chain.");
        }

        if (snapshot.StorageStatus.CriticalIssues.Count > 0)
        {
            return (HealthStatus.Critical, snapshot.StorageStatus.CriticalIssues[0], "Free up space or expand backup volume before next FULL backup.");
        }

        if (snapshot.LastBackupStatus.Warnings.Count > 0)
        {
            return (HealthStatus.Warning, snapshot.LastBackupStatus.Warnings[0], "Review warning and run missed backup job.");
        }

        return (HealthStatus.Healthy, "All backup monitors are healthy.", "No immediate action required.");
    }

    private string BuildNextRisk(DashboardSnapshot snapshot)
    {
        var storageCritical = snapshot.StorageStatus.CriticalIssues.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(storageCritical))
            return storageCritical;

        var backupCritical = snapshot.LastBackupStatus.CriticalIssues.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(backupCritical))
            return backupCritical;

        var storageWarn = snapshot.StorageStatus.Warnings.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(storageWarn))
            return storageWarn;

        var backupWarn = snapshot.LastBackupStatus.Warnings.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(backupWarn))
            return backupWarn;

        return "No immediate risk detected.";
    }

    private void panelTimeline_Paint(object? sender, PaintEventArgs e)
    {
        if (_panelTimeline == null)
            return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var chartRect = new Rectangle(20, 70, _panelTimeline.ClientSize.Width - 40, _panelTimeline.ClientSize.Height - 95);
        if (chartRect.Width <= 40 || chartRect.Height <= 40)
            return;

        using var axisPen = new Pen(Color.FromArgb(205, 213, 224), 1);
        using var fullBrush = new SolidBrush(Color.FromArgb(52, 102, 179));
        using var diffBrush = new SolidBrush(Color.FromArgb(47, 167, 163));
        using var logBrush = new SolidBrush(Color.FromArgb(235, 137, 52));
        using var failBrush = new SolidBrush(Color.FromArgb(183, 28, 28));

        g.DrawRectangle(axisPen, chartRect);

        if (_timelineJobs.Count == 0)
        {
            using var noDataBrush = new SolidBrush(Color.Gray);
            g.DrawString("No recent backup events", new Font("Segoe UI", 10F, FontStyle.Italic), noDataBrush, chartRect.Left + 12, chartRect.Top + 12);
            return;
        }

        var minTime = _timelineJobs.Min(j => j.StartTime!.Value);
        var maxTime = _timelineJobs.Max(j => j.StartTime!.Value);
        if (minTime == maxTime)
            maxTime = maxTime.AddMinutes(1);

        var lanes = new Dictionary<BackupType, int>
        {
            [BackupType.Full] = chartRect.Top + 25,
            [BackupType.Differential] = chartRect.Top + (chartRect.Height / 2),
            [BackupType.TransactionLog] = chartRect.Bottom - 25
        };

        using var textBrush = new SolidBrush(Color.FromArgb(74, 85, 104));
        g.DrawString("FULL", new Font("Segoe UI", 8F, FontStyle.Bold), textBrush, chartRect.Left + 6, lanes[BackupType.Full] - 10);
        g.DrawString("DIFF", new Font("Segoe UI", 8F, FontStyle.Bold), textBrush, chartRect.Left + 6, lanes[BackupType.Differential] - 10);
        g.DrawString("LOG", new Font("Segoe UI", 8F, FontStyle.Bold), textBrush, chartRect.Left + 6, lanes[BackupType.TransactionLog] - 10);

        foreach (var lane in lanes.Values)
        {
            g.DrawLine(axisPen, chartRect.Left + 45, lane, chartRect.Right - 8, lane);
        }

        foreach (var job in _timelineJobs)
        {
            if (!job.StartTime.HasValue || !lanes.ContainsKey(job.BackupType))
                continue;

            var ratio = (job.StartTime.Value - minTime).TotalSeconds / (maxTime - minTime).TotalSeconds;
            var x = chartRect.Left + 50 + (int)((chartRect.Width - 66) * ratio);
            var y = lanes[job.BackupType];

            var brush = job.Status == BackupStatus.Failed
                ? failBrush
                : job.BackupType switch
                {
                    BackupType.Full => fullBrush,
                    BackupType.Differential => diffBrush,
                    _ => logBrush
                };

            g.FillEllipse(brush, x - 4, y - 4, 8, 8);
        }

        var rangeText = $"{minTime.ToLocalTime():MM-dd HH:mm} - {maxTime.ToLocalTime():MM-dd HH:mm}";
        g.DrawString(rangeText, new Font("Segoe UI", 8F, FontStyle.Regular), textBrush, chartRect.Left + 45, chartRect.Bottom + 6);
    }

    private static string BuildJobMessage(RecentJobSummary job)
    {
        if (job.Status == BackupStatus.Failed)
            return string.IsNullOrWhiteSpace(job.ErrorMessage) ? "Failed" : job.ErrorMessage;

        if (job.Status == BackupStatus.Running)
            return "Job in progress";

        return "Completed";
    }

    private static string GetStatusIcon(BackupStatus status)
    {
        return status switch
        {
            BackupStatus.Completed => "[OK]",
            BackupStatus.Failed => "[X]",
            BackupStatus.Running => "[!]",
            _ => "[?]"
        };
    }

    private static string StatusBadgeText(HealthStatus health)
    {
        return health switch
        {
            HealthStatus.Healthy => "HEALTHY",
            HealthStatus.Warning => "WARNING",
            HealthStatus.Critical => "CRITICAL",
            _ => "UNKNOWN"
        };
    }

    private static string BuildChainMessage(LastBackupStatus status)
    {
        if (status.CriticalIssues.Count > 0)
            return status.CriticalIssues[0];

        if (status.Warnings.Count > 0)
            return status.Warnings[0];

        return status.ChainHealthSummary;
    }

    private static string BuildChainAction(LastBackupStatus status)
    {
        if (status.CriticalIssues.Count > 0)
            return "Action: Run FULL backup now and validate restore chain.";

        if (status.Warnings.Count > 0)
            return "Action: Run pending backup job and monitor next cycle.";

        return "Action: Continue normal monitoring.";
    }

    private static long? TryExtractEstimatedNextFullBytes(StorageStatusSummary storage)
    {
        var sourceMessages = storage.CriticalIssues.Concat(storage.Warnings);

        foreach (var message in sourceMessages)
        {
            var match = Regex.Match(message, @"(\d+(?:\.\d+)?)\s*(TB|GB|MB)", RegexOptions.IgnoreCase);
            if (!match.Success)
                continue;

            if (!double.TryParse(match.Groups[1].Value, out var numeric))
                continue;

            var unit = match.Groups[2].Value.ToUpperInvariant();
            var multiplier = unit switch
            {
                "TB" => 1024d * 1024d * 1024d * 1024d,
                "GB" => 1024d * 1024d * 1024d,
                "MB" => 1024d * 1024d,
                _ => 1d
            };

            return (long)(numeric * multiplier);
        }

        return null;
    }

    private static bool WillLikelyFailNextFull(StorageStatusSummary storage, long? estimate)
    {
        if (storage.OverallHealth == HealthStatus.Critical)
            return true;

        if (estimate.HasValue && storage.FreeBytes < estimate.Value)
            return true;

        return storage.CriticalIssues.Any(issue => issue.Contains("insufficient", StringComparison.OrdinalIgnoreCase)
            || issue.Contains("not enough", StringComparison.OrdinalIgnoreCase)
            || issue.Contains("fail", StringComparison.OrdinalIgnoreCase));
    }

    private static HealthStatus GetWorstHealth(params HealthStatus[] statuses)
    {
        if (statuses.Any(s => s == HealthStatus.Critical))
            return HealthStatus.Critical;

        if (statuses.Any(s => s == HealthStatus.Warning))
            return HealthStatus.Warning;

        return HealthStatus.Healthy;
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
