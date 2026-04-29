using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;

namespace Deadpool.UI;

/// <summary>
/// Backup job monitoring form - read-only history and failure visibility.
/// </summary>
public partial class BackupJobMonitorForm : Form
{
    private readonly IBackupJobMonitoringService _monitoringService;
    private readonly string _databaseName;
    private BackupJobFilter _currentFilter;
    private List<BackupJobDisplayModel> _currentJobs = new();

    public BackupJobMonitorForm(IBackupJobMonitoringService monitoringService, string databaseName)
    {
        _monitoringService = monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));
        _databaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));

        InitializeComponent();
        InitializeFilters();

        _currentFilter = new BackupJobFilter(_databaseName);
    }

    private void InitializeFilters()
    {
        // Backup Type filter
        cmbBackupType.Items.Add("All");
        cmbBackupType.Items.Add("Full");
        cmbBackupType.Items.Add("Differential");
        cmbBackupType.Items.Add("TransactionLog");
        cmbBackupType.SelectedIndex = 0;

        // Status filter
        cmbStatus.Items.Add("All");
        cmbStatus.Items.Add("Pending");
        cmbStatus.Items.Add("Running");
        cmbStatus.Items.Add("Completed");
        cmbStatus.Items.Add("Failed");
        cmbStatus.SelectedIndex = 0;

        // Date filter
        dtpStartDate.Value = DateTime.Today.AddDays(-7);
        dtpEndDate.Value = DateTime.Today;
    }

    private async void BackupJobMonitorForm_Load(object sender, EventArgs e)
    {
        await RefreshAsync();
    }

    private async void btnRefresh_Click(object sender, EventArgs e)
    {
        await RefreshAsync();
    }

    private async void btnApplyFilter_Click(object sender, EventArgs e)
    {
        BuildFilter();
        await RefreshAsync();
    }

    private async void btnClearFilter_Click(object sender, EventArgs e)
    {
        ClearFilters();
        await RefreshAsync();
    }

    private void chkEnableDateFilter_CheckedChanged(object sender, EventArgs e)
    {
        dtpStartDate.Enabled = chkEnableDateFilter.Checked;
        dtpEndDate.Enabled = chkEnableDateFilter.Checked;
    }

    private void BuildFilter()
    {
        _currentFilter = new BackupJobFilter
        {
            DatabaseName = _databaseName,
            BackupType = cmbBackupType.SelectedIndex > 0
                ? Enum.Parse<BackupType>(cmbBackupType.SelectedItem?.ToString() ?? "Full")
                : null,
            Status = cmbStatus.SelectedIndex > 0
                ? Enum.Parse<BackupStatus>(cmbStatus.SelectedItem?.ToString() ?? "Pending")
                : null,
            StartDate = chkEnableDateFilter.Checked ? dtpStartDate.Value.Date : null,
            EndDate = chkEnableDateFilter.Checked ? dtpEndDate.Value.Date : null,
            MaxResults = 100
        };
    }

    private void ClearFilters()
    {
        cmbBackupType.SelectedIndex = 0;
        cmbStatus.SelectedIndex = 0;
        chkEnableDateFilter.Checked = false;
        dtpStartDate.Value = DateTime.Today.AddDays(-7);
        dtpEndDate.Value = DateTime.Today;

        _currentFilter = new BackupJobFilter(_databaseName);
    }

    private async Task RefreshAsync()
    {
        try
        {
            btnRefresh.Enabled = false;
            lblLastRefresh.Text = "Refreshing...";

            // Get job history
            _currentJobs = await _monitoringService.GetBackupJobHistoryAsync(_currentFilter);

            // Get summary
            var summary = await _monitoringService.GetJobStatusSummaryAsync(_databaseName);

            // Display results
            DisplayJobs(_currentJobs);
            DisplaySummary(summary);

            lblLastRefresh.Text = $"Last refresh: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to load backup job history: {ex.Message}",
                "Load Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            lblLastRefresh.Text = "Refresh failed";
        }
        finally
        {
            btnRefresh.Enabled = true;
        }
    }

    private void DisplayJobs(List<BackupJobDisplayModel> jobs)
    {
        dgvJobs.DataSource = null;

        if (!jobs.Any())
        {
            txtErrorMessage.Text = "No backup jobs found matching the filter criteria.";
            return;
        }

        var displayData = jobs.Select(j => new
        {
            j.BackupType,
            j.Status,
            StartTime = j.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
            EndTime = j.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--",
            j.Duration,
            Size = j.FileSizeDisplay,
            FilePath = Path.GetFileName(j.FilePath)
        }).ToList();

        dgvJobs.DataSource = displayData;

        // Apply row coloring based on status
        foreach (DataGridViewRow row in dgvJobs.Rows)
        {
            var status = row.Cells["Status"].Value?.ToString();
            switch (status)
            {
                case "Completed":
                    row.DefaultCellStyle.BackColor = Color.LightGreen;
                    break;
                case "Failed":
                    row.DefaultCellStyle.BackColor = Color.LightCoral;
                    row.DefaultCellStyle.Font = new Font(dgvJobs.Font, FontStyle.Bold);
                    break;
                case "Running":
                    row.DefaultCellStyle.BackColor = Color.LightYellow;
                    break;
                case "Pending":
                    row.DefaultCellStyle.BackColor = Color.LightBlue;
                    break;
            }
        }

        // Auto-size columns
        dgvJobs.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
    }

    private void DisplaySummary(Dictionary<string, int> summary)
    {
        lblPending.Text = $"Pending: {summary["Pending"]}";
        lblRunning.Text = $"Running: {summary["Running"]}";
        lblCompleted.Text = $"Completed: {summary["Completed"]}";
        lblFailed.Text = $"Failed: {summary["Failed"]}";

        // Highlight failed count if > 0
        if (summary["Failed"] > 0)
        {
            lblFailed.Font = new Font(lblFailed.Font, FontStyle.Bold);
            lblFailed.ForeColor = Color.DarkRed;
        }
        else
        {
            lblFailed.Font = new Font(lblFailed.Font, FontStyle.Regular);
            lblFailed.ForeColor = Color.Green;
        }
    }

    private void dgvJobs_SelectionChanged(object sender, EventArgs e)
    {
        if (dgvJobs.SelectedRows.Count == 0 || !_currentJobs.Any())
        {
            txtErrorMessage.Text = "";
            return;
        }

        var selectedIndex = dgvJobs.SelectedRows[0].Index;
        if (selectedIndex < 0 || selectedIndex >= _currentJobs.Count)
        {
            txtErrorMessage.Text = "";
            return;
        }

        var selectedJob = _currentJobs[selectedIndex];

        // Build details display
        var details = $"Database: {selectedJob.DatabaseName}\r\n";
        details += $"Backup Type: {selectedJob.BackupType}\r\n";
        details += $"Status: {selectedJob.Status}\r\n";
        details += $"Start Time: {selectedJob.StartTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}\r\n";
        details += $"End Time: {selectedJob.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "In Progress"}\r\n";
        details += $"Duration: {selectedJob.Duration}\r\n";
        details += $"File Path: {selectedJob.FilePath}\r\n";
        details += $"File Size: {selectedJob.FileSizeDisplay}\r\n";

        if (!string.IsNullOrEmpty(selectedJob.ErrorMessage))
        {
            details += $"\r\n--- ERROR MESSAGE ---\r\n{selectedJob.ErrorMessage}";
        }

        txtErrorMessage.Text = details;
    }
}
