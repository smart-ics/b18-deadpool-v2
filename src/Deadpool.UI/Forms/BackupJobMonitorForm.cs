namespace Deadpool.UI.Forms;

public partial class BackupJobMonitorForm : Form
{
    public BackupJobMonitorForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "Backup Job Monitor";
        this.Size = new Size(1000, 600);
        this.StartPosition = FormStartPosition.CenterParent;

        var label = new Label
        {
            Text = "Backup Job Monitor - To be implemented",
            AutoSize = true,
            Location = new Point(20, 20),
            Font = new Font("Segoe UI", 12F)
        };

        this.Controls.Add(label);
    }
}
