using Microsoft.Extensions.DependencyInjection;

namespace Deadpool.UI.Forms;

public partial class MainForm : Form
{
    private readonly IServiceProvider _serviceProvider;

    public MainForm(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        InitializeComponent();
        this.Load += MainForm_Load;
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        UpdateStatus("Ready");
        LoadDashboard();
    }

    private void LoadDashboard()
    {
        dashboardPanel.Controls.Clear();
        
        var welcomeLabel = new Label
        {
            Text = "Welcome to Deadpool Backup Tools\n\n" +
                   "Phase 1: Backup & Monitoring\n\n" +
                   "• Manage SQL Server instances\n" +
                   "• Configure backup schedules\n" +
                   "• Monitor backup job execution\n" +
                   "• View backup health status",
            Font = new Font("Segoe UI", 12F, FontStyle.Regular),
            AutoSize = true,
            Location = new Point(20, 20)
        };

        dashboardPanel.Controls.Add(welcomeLabel);
    }

    private void ExitMenuItem_Click(object? sender, EventArgs e)
    {
        Application.Exit();
    }

    private void ManageServersMenuItem_Click(object? sender, EventArgs e)
    {
        var serverForm = _serviceProvider.GetRequiredService<ServerManagementForm>();
        serverForm.ShowDialog(this);
    }

    private void ManageSchedulesMenuItem_Click(object? sender, EventArgs e)
    {
        var scheduleForm = _serviceProvider.GetRequiredService<BackupScheduleForm>();
        scheduleForm.ShowDialog(this);
    }

    private void ViewJobsMenuItem_Click(object? sender, EventArgs e)
    {
        var jobMonitorForm = _serviceProvider.GetRequiredService<BackupJobMonitorForm>();
        jobMonitorForm.ShowDialog(this);
    }

    private void UpdateStatus(string message)
    {
        statusLabel.Text = $"{message} - {DateTime.Now:HH:mm:ss}";
    }
}
