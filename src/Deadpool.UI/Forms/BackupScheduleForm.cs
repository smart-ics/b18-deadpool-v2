namespace Deadpool.UI.Forms;

public partial class BackupScheduleForm : Form
{
    public BackupScheduleForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "Backup Schedule Management";
        this.Size = new Size(800, 600);
        this.StartPosition = FormStartPosition.CenterParent;

        var label = new Label
        {
            Text = "Backup Schedule Management - To be implemented",
            AutoSize = true,
            Location = new Point(20, 20),
            Font = new Font("Segoe UI", 12F)
        };

        this.Controls.Add(label);
    }
}
