namespace Deadpool.UI.Forms;

public partial class ServerManagementForm : Form
{
    public ServerManagementForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "SQL Server Management";
        this.Size = new Size(800, 600);
        this.StartPosition = FormStartPosition.CenterParent;

        var label = new Label
        {
            Text = "Server Management - To be implemented",
            AutoSize = true,
            Location = new Point(20, 20),
            Font = new Font("Segoe UI", 12F)
        };

        this.Controls.Add(label);
    }
}
