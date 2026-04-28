namespace Deadpool.UI.Forms
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.menuStrip = new MenuStrip();
            this.fileMenuItem = new ToolStripMenuItem();
            this.exitMenuItem = new ToolStripMenuItem();
            this.serversMenuItem = new ToolStripMenuItem();
            this.manageServersMenuItem = new ToolStripMenuItem();
            this.backupsMenuItem = new ToolStripMenuItem();
            this.manageSchedulesMenuItem = new ToolStripMenuItem();
            this.viewJobsMenuItem = new ToolStripMenuItem();
            this.runAdHocBackupMenuItem = new ToolStripMenuItem();
            this.helpMenuItem = new ToolStripMenuItem();
            this.aboutMenuItem = new ToolStripMenuItem();
            this.statusStrip = new StatusStrip();
            this.statusLabel = new ToolStripStatusLabel();
            this.tabControl = new TabControl();
            this.dashboardTab = new TabPage();
            this.dashboardPanel = new Panel();
            this.serversTab = new TabPage();
            this.serversPanel = new Panel();
            this.jobsTab = new TabPage();
            this.jobsPanel = new Panel();

            this.menuStrip.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.tabControl.SuspendLayout();
            this.SuspendLayout();

            // menuStrip
            this.menuStrip.Items.AddRange(new ToolStripItem[] {
                this.fileMenuItem,
                this.serversMenuItem,
                this.backupsMenuItem,
                this.helpMenuItem});
            this.menuStrip.Location = new Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Size = new Size(1200, 24);

            // File Menu
            this.fileMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.exitMenuItem });
            this.fileMenuItem.Name = "fileMenuItem";
            this.fileMenuItem.Size = new Size(37, 20);
            this.fileMenuItem.Text = "&File";

            this.exitMenuItem.Name = "exitMenuItem";
            this.exitMenuItem.Size = new Size(93, 22);
            this.exitMenuItem.Text = "E&xit";
            this.exitMenuItem.Click += new EventHandler(this.ExitMenuItem_Click);

            // Servers Menu
            this.serversMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.manageServersMenuItem });
            this.serversMenuItem.Name = "serversMenuItem";
            this.serversMenuItem.Size = new Size(56, 20);
            this.serversMenuItem.Text = "&Servers";

            this.manageServersMenuItem.Name = "manageServersMenuItem";
            this.manageServersMenuItem.Size = new Size(165, 22);
            this.manageServersMenuItem.Text = "&Manage Servers";
            this.manageServersMenuItem.Click += new EventHandler(this.ManageServersMenuItem_Click);

            // Backups Menu
            this.backupsMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
                this.manageSchedulesMenuItem,
                this.viewJobsMenuItem,
                this.runAdHocBackupMenuItem});
            this.backupsMenuItem.Name = "backupsMenuItem";
            this.backupsMenuItem.Size = new Size(63, 20);
            this.backupsMenuItem.Text = "&Backups";

            this.manageSchedulesMenuItem.Name = "manageSchedulesMenuItem";
            this.manageSchedulesMenuItem.Size = new Size(180, 22);
            this.manageSchedulesMenuItem.Text = "Manage &Schedules";
            this.manageSchedulesMenuItem.Click += new EventHandler(this.ManageSchedulesMenuItem_Click);

            this.viewJobsMenuItem.Name = "viewJobsMenuItem";
            this.viewJobsMenuItem.Size = new Size(180, 22);
            this.viewJobsMenuItem.Text = "View &Jobs";
            this.viewJobsMenuItem.Click += new EventHandler(this.ViewJobsMenuItem_Click);

            this.runAdHocBackupMenuItem.Name = "runAdHocBackupMenuItem";
            this.runAdHocBackupMenuItem.Size = new Size(180, 22);
            this.runAdHocBackupMenuItem.Text = "&Run Ad-Hoc Backup";

            // Help Menu
            this.helpMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.aboutMenuItem });
            this.helpMenuItem.Name = "helpMenuItem";
            this.helpMenuItem.Size = new Size(44, 20);
            this.helpMenuItem.Text = "&Help";

            this.aboutMenuItem.Name = "aboutMenuItem";
            this.aboutMenuItem.Size = new Size(107, 22);
            this.aboutMenuItem.Text = "&About";

            // statusStrip
            this.statusStrip.Items.AddRange(new ToolStripItem[] { this.statusLabel });
            this.statusStrip.Location = new Point(0, 726);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new Size(1200, 22);

            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new Size(39, 17);
            this.statusLabel.Text = "Ready";

            // tabControl
            this.tabControl.Controls.Add(this.dashboardTab);
            this.tabControl.Controls.Add(this.serversTab);
            this.tabControl.Controls.Add(this.jobsTab);
            this.tabControl.Dock = DockStyle.Fill;
            this.tabControl.Location = new Point(0, 24);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new Size(1200, 702);

            // Dashboard Tab
            this.dashboardTab.Controls.Add(this.dashboardPanel);
            this.dashboardTab.Location = new Point(4, 24);
            this.dashboardTab.Name = "dashboardTab";
            this.dashboardTab.Padding = new Padding(3);
            this.dashboardTab.Size = new Size(1192, 674);
            this.dashboardTab.Text = "Dashboard";
            this.dashboardTab.UseVisualStyleBackColor = true;

            this.dashboardPanel.Dock = DockStyle.Fill;
            this.dashboardPanel.Location = new Point(3, 3);
            this.dashboardPanel.Name = "dashboardPanel";
            this.dashboardPanel.Size = new Size(1186, 668);

            // Servers Tab
            this.serversTab.Controls.Add(this.serversPanel);
            this.serversTab.Location = new Point(4, 24);
            this.serversTab.Name = "serversTab";
            this.serversTab.Padding = new Padding(3);
            this.serversTab.Size = new Size(1192, 674);
            this.serversTab.Text = "Servers";
            this.serversTab.UseVisualStyleBackColor = true;

            this.serversPanel.Dock = DockStyle.Fill;
            this.serversPanel.Location = new Point(3, 3);
            this.serversPanel.Name = "serversPanel";
            this.serversPanel.Size = new Size(1186, 668);

            // Jobs Tab
            this.jobsTab.Controls.Add(this.jobsPanel);
            this.jobsTab.Location = new Point(4, 24);
            this.jobsTab.Name = "jobsTab";
            this.jobsTab.Padding = new Padding(3);
            this.jobsTab.Size = new Size(1192, 674);
            this.jobsTab.Text = "Backup Jobs";
            this.jobsTab.UseVisualStyleBackColor = true;

            this.jobsPanel.Dock = DockStyle.Fill;
            this.jobsPanel.Location = new Point(3, 3);
            this.jobsPanel.Name = "jobsPanel";
            this.jobsPanel.Size = new Size(1186, 668);

            // MainForm
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1200, 748);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.menuStrip);
            this.MainMenuStrip = this.menuStrip;
            this.Name = "MainForm";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Deadpool Backup Tools - SQL Server Backup & Recovery";

            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.tabControl.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private MenuStrip menuStrip;
        private ToolStripMenuItem fileMenuItem;
        private ToolStripMenuItem exitMenuItem;
        private ToolStripMenuItem serversMenuItem;
        private ToolStripMenuItem manageServersMenuItem;
        private ToolStripMenuItem backupsMenuItem;
        private ToolStripMenuItem manageSchedulesMenuItem;
        private ToolStripMenuItem viewJobsMenuItem;
        private ToolStripMenuItem runAdHocBackupMenuItem;
        private ToolStripMenuItem helpMenuItem;
        private ToolStripMenuItem aboutMenuItem;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;
        private TabControl tabControl;
        private TabPage dashboardTab;
        private Panel dashboardPanel;
        private TabPage serversTab;
        private Panel serversPanel;
        private TabPage jobsTab;
        private Panel jobsPanel;
    }
}
