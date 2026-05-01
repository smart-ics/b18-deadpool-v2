namespace Deadpool.UI
{
    partial class MonitoringDashboard
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _autoRefreshTimer?.Stop();
                _autoRefreshTimer?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            panelHeader = new Panel();
            lblTitle = new Label();
            lblSystemStatus = new Label();
            lblDbSummary = new Label();
            lblNextRisk = new Label();
            lblLastRefresh = new Label();
            btnJobMonitor = new Button();
            btnRefresh = new Button();
            tableBody = new TableLayoutPanel();
            tableLeftCards = new TableLayoutPanel();
            tableMainBottom = new TableLayoutPanel();
            panelLastBackupStatus = new Panel();
            lblLastBackupTitle = new Label();
            lblFullBackup = new Label();
            lblDiffBackup = new Label();
            lblLogBackup = new Label();
            panelChainHealthCard = new Panel();
            lblChainHealthTitle = new Label();
            lblChainHealth = new Label();
            lblChainMessage = new Label();
            lblChainAction = new Label();
            panelStorageStatus = new Panel();
            lblStorageTitle = new Label();
            lblStoragePath = new Label();
            lblStorageSpace = new Label();
            lblEstimatedFullBackup = new Label();
            lblStoragePrediction = new Label();
            progressBarStorage = new ProgressBar();
            panelDatabaseCard = new Panel();
            lblDatabaseCardTitle = new Label();
            lblDatabaseStatus = new Label();
            panelAlert = new Panel();
            lblAlertTitle = new Label();
            lblAlertMessage = new Label();
            lblAlertAction = new Label();
            panelRecentJobs = new Panel();
            lblRecentJobsTitle = new Label();
            dgvRecentJobs = new DataGridView();
            panelDetails = new Panel();
            tableDetails = new TableLayoutPanel();
            panelBackupPolicy = new Panel();
            lblPolicyTitle = new Label();
            lblPolicyFullSchedule = new Label();
            lblPolicyDifferentialSchedule = new Label();
            lblPolicyLogSchedule = new Label();
            lblPolicyRecoveryModel = new Label();
            lblPolicyRetention = new Label();
            lblPolicyBootstrap = new Label();
            panelDatabaseTopology = new Panel();
            lblDatabaseTopologyTitle = new Label();
            lblDbName = new Label();
            lblDbServer = new Label();
            lblDbRecoveryModel = new Label();
            lblTopologyProdServer = new Label();
            lblTopologyBackupServer = new Label();
            lblTopologyDestinationPath = new Label();
            lblPulseStatus = new Label();
            lblPulseLastChecked = new Label();
            lblChainInitialized = new Label();
            lblLastValidFullBackup = new Label();
            lblRestoreChainHealthSimple = new Label();
            lblChainInitializationWarning = new Label();
            panelHeader.SuspendLayout();
            tableBody.SuspendLayout();
            tableLeftCards.SuspendLayout();
            tableMainBottom.SuspendLayout();
            panelLastBackupStatus.SuspendLayout();
            panelChainHealthCard.SuspendLayout();
            panelStorageStatus.SuspendLayout();
            panelDatabaseCard.SuspendLayout();
            panelAlert.SuspendLayout();
            panelRecentJobs.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvRecentJobs).BeginInit();
            panelDetails.SuspendLayout();
            tableDetails.SuspendLayout();
            panelBackupPolicy.SuspendLayout();
            panelDatabaseTopology.SuspendLayout();
            SuspendLayout();
            // 
            // panelHeader
            // 
            panelHeader.BackColor = Color.FromArgb(27, 44, 67);
            panelHeader.Controls.Add(lblSystemStatus);
            panelHeader.Controls.Add(lblDbSummary);
            panelHeader.Controls.Add(lblNextRisk);
            panelHeader.Controls.Add(lblLastRefresh);
            panelHeader.Controls.Add(btnJobMonitor);
            panelHeader.Controls.Add(btnRefresh);
            panelHeader.Dock = DockStyle.Top;
            panelHeader.Location = new Point(0, 0);
            panelHeader.Name = "panelHeader";
            panelHeader.Size = new Size(1480, 52);
            panelHeader.TabIndex = 0;
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(12, 16);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(91, 19);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "Deadpool";
            lblTitle.Visible = false;
            // 
            // lblSystemStatus
            // 
            lblSystemStatus.AutoSize = true;
            lblSystemStatus.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            lblSystemStatus.ForeColor = Color.FromArgb(255, 199, 44);
            lblSystemStatus.Location = new Point(12, 15);
            lblSystemStatus.Name = "lblSystemStatus";
            lblSystemStatus.Size = new Size(108, 21);
            lblSystemStatus.TabIndex = 1;
            lblSystemStatus.Text = "X CRITICAL";
            // 
            // lblDbSummary
            // 
            lblDbSummary.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblDbSummary.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblDbSummary.ForeColor = Color.FromArgb(224, 233, 245);
            lblDbSummary.Location = new Point(780, 18);
            lblDbSummary.Name = "lblDbSummary";
            lblDbSummary.Size = new Size(308, 16);
            lblDbSummary.TabIndex = 2;
            lblDbSummary.Text = "Next Backup: FULL Sun 00:00 | DIFF 01:00 | LOG +12m";
            lblDbSummary.TextAlign = ContentAlignment.MiddleRight;
            // 
            // lblNextRisk
            // 
            lblNextRisk.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblNextRisk.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblNextRisk.ForeColor = Color.FromArgb(255, 219, 153);
            lblNextRisk.Location = new Point(168, 16);
            lblNextRisk.Name = "lblNextRisk";
            lblNextRisk.Size = new Size(606, 20);
            lblNextRisk.TabIndex = 3;
            lblNextRisk.Text = "Storage will fail";
            // 
            // lblLastRefresh
            // 
            lblLastRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblLastRefresh.ForeColor = Color.FromArgb(214, 220, 230);
            lblLastRefresh.Location = new Point(1092, 3);
            lblLastRefresh.Name = "lblLastRefresh";
            lblLastRefresh.Size = new Size(278, 14);
            lblLastRefresh.TabIndex = 4;
            lblLastRefresh.Text = "Last refresh: --";
            lblLastRefresh.TextAlign = ContentAlignment.MiddleRight;
            // 
            // btnJobMonitor
            // 
            btnJobMonitor.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnJobMonitor.BackColor = Color.FromArgb(58, 85, 117);
            btnJobMonitor.FlatStyle = FlatStyle.Flat;
            btnJobMonitor.ForeColor = Color.White;
            btnJobMonitor.Location = new Point(1092, 19);
            btnJobMonitor.Name = "btnJobMonitor";
            btnJobMonitor.Size = new Size(138, 28);
            btnJobMonitor.TabIndex = 5;
            btnJobMonitor.Text = "Job Monitor";
            btnJobMonitor.UseVisualStyleBackColor = false;
            btnJobMonitor.Click += btnJobMonitor_Click;
            // 
            // btnRefresh
            // 
            btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRefresh.BackColor = Color.FromArgb(17, 125, 187);
            btnRefresh.FlatStyle = FlatStyle.Flat;
            btnRefresh.ForeColor = Color.White;
            btnRefresh.Location = new Point(1236, 19);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(108, 28);
            btnRefresh.TabIndex = 6;
            btnRefresh.Text = "Refresh";
            btnRefresh.UseVisualStyleBackColor = false;
            btnRefresh.Click += btnRefresh_Click;
            // 
            // tableBody
            // 
            tableBody.ColumnCount = 1;
            tableBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableBody.Controls.Add(tableLeftCards, 0, 0);
            tableBody.Controls.Add(tableMainBottom, 0, 1);
            tableBody.Dock = DockStyle.Fill;
            tableBody.Location = new Point(0, 52);
            tableBody.Name = "tableBody";
            tableBody.Padding = new Padding(8, 6, 8, 8);
            tableBody.RowCount = 2;
            tableBody.RowStyles.Add(new RowStyle(SizeType.Absolute, 104F));
            tableBody.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableBody.Size = new Size(1480, 898);
            tableBody.TabIndex = 1;
            // 
            // tableLeftCards
            // 
            tableLeftCards.ColumnCount = 4;
            tableLeftCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tableLeftCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tableLeftCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tableLeftCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tableLeftCards.Controls.Add(panelLastBackupStatus, 0, 0);
            tableLeftCards.Controls.Add(panelChainHealthCard, 1, 0);
            tableLeftCards.Controls.Add(panelStorageStatus, 2, 0);
            tableLeftCards.Controls.Add(panelDatabaseCard, 3, 0);
            tableLeftCards.Dock = DockStyle.Fill;
            tableLeftCards.Location = new Point(11, 9);
            tableLeftCards.Name = "tableLeftCards";
            tableLeftCards.RowCount = 1;
            tableLeftCards.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLeftCards.Size = new Size(1458, 98);
            tableLeftCards.TabIndex = 0;
            // 
            // tableMainBottom
            // 
            tableMainBottom.ColumnCount = 2;
            tableMainBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 73F));
            tableMainBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27F));
            tableMainBottom.Controls.Add(panelRecentJobs, 0, 0);
            tableMainBottom.Controls.Add(panelAlert, 1, 0);
            tableMainBottom.Dock = DockStyle.Fill;
            tableMainBottom.Location = new Point(11, 113);
            tableMainBottom.Name = "tableMainBottom";
            tableMainBottom.RowCount = 1;
            tableMainBottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableMainBottom.Size = new Size(1458, 776);
            tableMainBottom.TabIndex = 1;
            // 
            // panelLastBackupStatus
            // 
            panelLastBackupStatus.BackColor = Color.White;
            panelLastBackupStatus.BorderStyle = BorderStyle.FixedSingle;
            panelLastBackupStatus.Controls.Add(lblLastBackupTitle);
            panelLastBackupStatus.Controls.Add(lblFullBackup);
            panelLastBackupStatus.Controls.Add(lblDiffBackup);
            panelLastBackupStatus.Controls.Add(lblLogBackup);
            panelLastBackupStatus.Dock = DockStyle.Fill;
            panelLastBackupStatus.Location = new Point(2, 2);
            panelLastBackupStatus.Name = "panelLastBackupStatus";
            panelLastBackupStatus.Padding = new Padding(8, 6, 8, 6);
            panelLastBackupStatus.Size = new Size(360, 94);
            panelLastBackupStatus.TabIndex = 0;
            // 
            // lblLastBackupTitle
            // 
            lblLastBackupTitle.AutoSize = true;
            lblLastBackupTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblLastBackupTitle.Location = new Point(8, 6);
            lblLastBackupTitle.Name = "lblLastBackupTitle";
            lblLastBackupTitle.Size = new Size(88, 19);
            lblLastBackupTitle.TabIndex = 0;
            lblLastBackupTitle.Text = "Last Backup";
            // 
            // lblFullBackup
            // 
            lblFullBackup.AutoSize = true;
            lblFullBackup.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblFullBackup.Location = new Point(8, 27);
            lblFullBackup.Name = "lblFullBackup";
            lblFullBackup.Size = new Size(96, 17);
            lblFullBackup.TabIndex = 1;
            lblFullBackup.Text = "FULL  Unknown";
            // 
            // lblDiffBackup
            // 
            lblDiffBackup.AutoSize = true;
            lblDiffBackup.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblDiffBackup.Location = new Point(8, 48);
            lblDiffBackup.Name = "lblDiffBackup";
            lblDiffBackup.Size = new Size(95, 17);
            lblDiffBackup.TabIndex = 2;
            lblDiffBackup.Text = "DIFF  Unknown";
            // 
            // lblLogBackup
            // 
            lblLogBackup.AutoSize = true;
            lblLogBackup.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblLogBackup.Location = new Point(8, 69);
            lblLogBackup.Name = "lblLogBackup";
            lblLogBackup.Size = new Size(90, 17);
            lblLogBackup.TabIndex = 3;
            lblLogBackup.Text = "LOG   Unknown";
            // 
            // panelChainHealthCard
            // 
            panelChainHealthCard.BackColor = Color.White;
            panelChainHealthCard.BorderStyle = BorderStyle.FixedSingle;
            panelChainHealthCard.Controls.Add(lblChainHealthTitle);
            panelChainHealthCard.Controls.Add(lblChainHealth);
            panelChainHealthCard.Controls.Add(lblChainMessage);
            panelChainHealthCard.Controls.Add(lblChainAction);
            panelChainHealthCard.Dock = DockStyle.Fill;
            panelChainHealthCard.Location = new Point(366, 2);
            panelChainHealthCard.Name = "panelChainHealthCard";
            panelChainHealthCard.Padding = new Padding(8, 6, 8, 6);
            panelChainHealthCard.Size = new Size(360, 94);
            panelChainHealthCard.TabIndex = 1;
            // 
            // lblChainHealthTitle
            // 
            lblChainHealthTitle.AutoSize = true;
            lblChainHealthTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblChainHealthTitle.Location = new Point(8, 6);
            lblChainHealthTitle.Name = "lblChainHealthTitle";
            lblChainHealthTitle.Size = new Size(95, 19);
            lblChainHealthTitle.TabIndex = 0;
            lblChainHealthTitle.Text = "Chain Health";
            // 
            // lblChainHealth
            // 
            lblChainHealth.AutoSize = true;
            lblChainHealth.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblChainHealth.Location = new Point(8, 27);
            lblChainHealth.Name = "lblChainHealth";
            lblChainHealth.Size = new Size(81, 20);
            lblChainHealth.TabIndex = 1;
            lblChainHealth.Text = "UNKNOWN";
            // 
            // lblChainMessage
            // 
            lblChainMessage.Location = new Point(8, 47);
            lblChainMessage.Name = "lblChainMessage";
            lblChainMessage.Size = new Size(344, 18);
            lblChainMessage.TabIndex = 2;
            lblChainMessage.Text = "Waiting for chain health analysis";
            // 
            // lblChainAction
            // 
            lblChainAction.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblChainAction.ForeColor = Color.FromArgb(48, 72, 97);
            lblChainAction.Location = new Point(8, 65);
            lblChainAction.Name = "lblChainAction";
            lblChainAction.Size = new Size(344, 18);
            lblChainAction.TabIndex = 3;
            lblChainAction.Text = "Action: --";
            // 
            // panelStorageStatus
            // 
            panelStorageStatus.BackColor = Color.White;
            panelStorageStatus.BorderStyle = BorderStyle.FixedSingle;
            panelStorageStatus.Controls.Add(lblStorageTitle);
            panelStorageStatus.Controls.Add(lblStoragePath);
            panelStorageStatus.Controls.Add(lblStorageSpace);
            panelStorageStatus.Controls.Add(lblEstimatedFullBackup);
            panelStorageStatus.Controls.Add(lblStoragePrediction);
            panelStorageStatus.Controls.Add(progressBarStorage);
            panelStorageStatus.Dock = DockStyle.Fill;
            panelStorageStatus.Location = new Point(730, 2);
            panelStorageStatus.Name = "panelStorageStatus";
            panelStorageStatus.Padding = new Padding(8, 6, 8, 6);
            panelStorageStatus.Size = new Size(360, 94);
            panelStorageStatus.TabIndex = 2;
            // 
            // lblStorageTitle
            // 
            lblStorageTitle.AutoSize = true;
            lblStorageTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblStorageTitle.Location = new Point(8, 6);
            lblStorageTitle.Name = "lblStorageTitle";
            lblStorageTitle.Size = new Size(56, 19);
            lblStorageTitle.TabIndex = 0;
            lblStorageTitle.Text = "Storage";
            // 
            // lblStoragePath
            // 
            lblStoragePath.Location = new Point(8, 25);
            lblStoragePath.Name = "lblStoragePath";
            lblStoragePath.Size = new Size(344, 15);
            lblStoragePath.TabIndex = 1;
            lblStoragePath.Text = "Volume: --";
            // 
            // lblStorageSpace
            // 
            lblStorageSpace.AutoSize = true;
            lblStorageSpace.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblStorageSpace.Location = new Point(8, 42);
            lblStorageSpace.Name = "lblStorageSpace";
            lblStorageSpace.Size = new Size(88, 15);
            lblStorageSpace.TabIndex = 2;
            lblStorageSpace.Text = "Free: Unknown";
            // 
            // lblEstimatedFullBackup
            // 
            lblEstimatedFullBackup.AutoSize = true;
            lblEstimatedFullBackup.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblEstimatedFullBackup.Location = new Point(8, 57);
            lblEstimatedFullBackup.Name = "lblEstimatedFullBackup";
            lblEstimatedFullBackup.Size = new Size(107, 15);
            lblEstimatedFullBackup.TabIndex = 3;
            lblEstimatedFullBackup.Text = "Next FULL: -- GB";
            // 
            // lblStoragePrediction
            // 
            lblStoragePrediction.AutoSize = true;
            lblStoragePrediction.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblStoragePrediction.Location = new Point(8, 72);
            lblStoragePrediction.Name = "lblStoragePrediction";
            lblStoragePrediction.Size = new Size(75, 19);
            lblStoragePrediction.TabIndex = 4;
            lblStoragePrediction.Text = "Status: --";
            // 
            // progressBarStorage
            // 
            progressBarStorage.Location = new Point(188, 74);
            progressBarStorage.Name = "progressBarStorage";
            progressBarStorage.Size = new Size(164, 10);
            progressBarStorage.TabIndex = 5;
            // 
            // panelDatabaseCard
            // 
            panelDatabaseCard.BackColor = Color.White;
            panelDatabaseCard.BorderStyle = BorderStyle.FixedSingle;
            panelDatabaseCard.Controls.Add(lblDatabaseCardTitle);
            panelDatabaseCard.Controls.Add(lblDatabaseStatus);
            panelDatabaseCard.Dock = DockStyle.Fill;
            panelDatabaseCard.Location = new Point(1094, 2);
            panelDatabaseCard.Name = "panelDatabaseCard";
            panelDatabaseCard.Padding = new Padding(8, 6, 8, 6);
            panelDatabaseCard.Size = new Size(362, 94);
            panelDatabaseCard.TabIndex = 3;
            // 
            // lblDatabaseCardTitle
            // 
            lblDatabaseCardTitle.AutoSize = true;
            lblDatabaseCardTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblDatabaseCardTitle.Location = new Point(8, 6);
            lblDatabaseCardTitle.Name = "lblDatabaseCardTitle";
            lblDatabaseCardTitle.Size = new Size(69, 19);
            lblDatabaseCardTitle.TabIndex = 0;
            lblDatabaseCardTitle.Text = "Database";
            // 
            // lblDatabaseStatus
            // 
            lblDatabaseStatus.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            lblDatabaseStatus.Location = new Point(8, 34);
            lblDatabaseStatus.Name = "lblDatabaseStatus";
            lblDatabaseStatus.Size = new Size(344, 42);
            lblDatabaseStatus.TabIndex = 1;
            lblDatabaseStatus.Text = "? Unknown";
            // 
            // panelAlert
            // 
            panelAlert.BackColor = Color.White;
            panelAlert.BorderStyle = BorderStyle.FixedSingle;
            panelAlert.Controls.Add(lblAlertTitle);
            panelAlert.Controls.Add(lblAlertMessage);
            panelAlert.Controls.Add(lblAlertAction);
            panelAlert.Dock = DockStyle.Fill;
            panelAlert.Location = new Point(1066, 3);
            panelAlert.Name = "panelAlert";
            panelAlert.Padding = new Padding(8, 6, 8, 6);
            panelAlert.Size = new Size(389, 770);
            panelAlert.TabIndex = 1;
            // 
            // lblAlertTitle
            // 
            lblAlertTitle.AutoSize = true;
            lblAlertTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblAlertTitle.Location = new Point(8, 6);
            lblAlertTitle.Name = "lblAlertTitle";
            lblAlertTitle.Size = new Size(43, 19);
            lblAlertTitle.TabIndex = 0;
            lblAlertTitle.Text = "Alerts";
            // 
            // lblAlertMessage
            // 
            lblAlertMessage.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblAlertMessage.Location = new Point(8, 28);
            lblAlertMessage.Name = "lblAlertMessage";
            lblAlertMessage.Size = new Size(370, 38);
            lblAlertMessage.TabIndex = 1;
            lblAlertMessage.Text = "Waiting for first refresh";
            // 
            // lblAlertAction
            // 
            lblAlertAction.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblAlertAction.ForeColor = Color.FromArgb(48, 72, 97);
            lblAlertAction.Location = new Point(8, 66);
            lblAlertAction.Name = "lblAlertAction";
            lblAlertAction.Size = new Size(370, 38);
            lblAlertAction.TabIndex = 2;
            lblAlertAction.Text = "-> --";
            // 
            // panelRecentJobs
            // 
            panelRecentJobs.BackColor = Color.White;
            panelRecentJobs.BorderStyle = BorderStyle.FixedSingle;
            panelRecentJobs.Controls.Add(dgvRecentJobs);
            panelRecentJobs.Controls.Add(lblRecentJobsTitle);
            panelRecentJobs.Dock = DockStyle.Fill;
            panelRecentJobs.Location = new Point(3, 3);
            panelRecentJobs.Name = "panelRecentJobs";
            panelRecentJobs.Padding = new Padding(8, 6, 8, 6);
            panelRecentJobs.Size = new Size(1057, 770);
            panelRecentJobs.TabIndex = 0;
            // 
            // lblRecentJobsTitle
            // 
            lblRecentJobsTitle.Dock = DockStyle.Top;
            lblRecentJobsTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblRecentJobsTitle.Location = new Point(8, 6);
            lblRecentJobsTitle.Name = "lblRecentJobsTitle";
            lblRecentJobsTitle.Size = new Size(1039, 22);
            lblRecentJobsTitle.TabIndex = 0;
            lblRecentJobsTitle.Text = "Recent Jobs";
            // 
            // dgvRecentJobs
            // 
            dgvRecentJobs.AllowUserToAddRows = false;
            dgvRecentJobs.AllowUserToDeleteRows = false;
            dgvRecentJobs.AllowUserToResizeRows = false;
            dgvRecentJobs.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dgvRecentJobs.BackgroundColor = Color.White;
            dgvRecentJobs.BorderStyle = BorderStyle.None;
            dgvRecentJobs.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvRecentJobs.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvRecentJobs.Dock = DockStyle.Fill;
            dgvRecentJobs.Location = new Point(8, 28);
            dgvRecentJobs.MultiSelect = false;
            dgvRecentJobs.Name = "dgvRecentJobs";
            dgvRecentJobs.ReadOnly = true;
            dgvRecentJobs.RowHeadersVisible = false;
            dgvRecentJobs.RowTemplate.Height = 22;
            dgvRecentJobs.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvRecentJobs.Size = new Size(1039, 736);
            dgvRecentJobs.TabIndex = 1;
            // 
            // panelDetails
            // 
            panelDetails.BackColor = Color.FromArgb(243, 246, 250);
            panelDetails.Controls.Add(tableDetails);
            panelDetails.Dock = DockStyle.Bottom;
            panelDetails.Location = new Point(0, 700);
            panelDetails.Name = "panelDetails";
            panelDetails.Padding = new Padding(12, 0, 12, 12);
            panelDetails.Size = new Size(1480, 250);
            panelDetails.TabIndex = 2;
            panelDetails.Visible = false;
            // 
            // tableDetails
            // 
            tableDetails.ColumnCount = 2;
            tableDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            tableDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            tableDetails.Controls.Add(panelBackupPolicy, 0, 0);
            tableDetails.Controls.Add(panelDatabaseTopology, 1, 0);
            tableDetails.Dock = DockStyle.Fill;
            tableDetails.Location = new Point(12, 0);
            tableDetails.Name = "tableDetails";
            tableDetails.RowCount = 1;
            tableDetails.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableDetails.Size = new Size(1456, 238);
            tableDetails.TabIndex = 0;
            // 
            // panelBackupPolicy
            // 
            panelBackupPolicy.BackColor = Color.White;
            panelBackupPolicy.BorderStyle = BorderStyle.FixedSingle;
            panelBackupPolicy.Controls.Add(lblPolicyTitle);
            panelBackupPolicy.Controls.Add(lblPolicyFullSchedule);
            panelBackupPolicy.Controls.Add(lblPolicyDifferentialSchedule);
            panelBackupPolicy.Controls.Add(lblPolicyLogSchedule);
            panelBackupPolicy.Controls.Add(lblPolicyRecoveryModel);
            panelBackupPolicy.Controls.Add(lblPolicyRetention);
            panelBackupPolicy.Controls.Add(lblPolicyBootstrap);
            panelBackupPolicy.Dock = DockStyle.Fill;
            panelBackupPolicy.Location = new Point(3, 3);
            panelBackupPolicy.Name = "panelBackupPolicy";
            panelBackupPolicy.Padding = new Padding(10);
            panelBackupPolicy.Size = new Size(649, 232);
            panelBackupPolicy.TabIndex = 0;
            // 
            // lblPolicyTitle
            // 
            lblPolicyTitle.AutoSize = true;
            lblPolicyTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblPolicyTitle.Location = new Point(10, 8);
            lblPolicyTitle.Name = "lblPolicyTitle";
            lblPolicyTitle.Size = new Size(105, 20);
            lblPolicyTitle.TabIndex = 0;
            lblPolicyTitle.Text = "Backup Policy";
            // 
            // lblPolicyFullSchedule
            // 
            lblPolicyFullSchedule.Location = new Point(10, 34);
            lblPolicyFullSchedule.Name = "lblPolicyFullSchedule";
            lblPolicyFullSchedule.Size = new Size(620, 26);
            lblPolicyFullSchedule.TabIndex = 1;
            lblPolicyFullSchedule.Text = "Full Backup runs --";
            // 
            // lblPolicyDifferentialSchedule
            // 
            lblPolicyDifferentialSchedule.Location = new Point(10, 58);
            lblPolicyDifferentialSchedule.Name = "lblPolicyDifferentialSchedule";
            lblPolicyDifferentialSchedule.Size = new Size(620, 26);
            lblPolicyDifferentialSchedule.TabIndex = 2;
            lblPolicyDifferentialSchedule.Text = "Differential Backup runs --";
            // 
            // lblPolicyLogSchedule
            // 
            lblPolicyLogSchedule.Location = new Point(10, 82);
            lblPolicyLogSchedule.Name = "lblPolicyLogSchedule";
            lblPolicyLogSchedule.Size = new Size(620, 26);
            lblPolicyLogSchedule.TabIndex = 3;
            lblPolicyLogSchedule.Text = "Transaction Log Backup runs --";
            // 
            // lblPolicyRecoveryModel
            // 
            lblPolicyRecoveryModel.AutoSize = true;
            lblPolicyRecoveryModel.Location = new Point(10, 112);
            lblPolicyRecoveryModel.Name = "lblPolicyRecoveryModel";
            lblPolicyRecoveryModel.Size = new Size(108, 15);
            lblPolicyRecoveryModel.TabIndex = 4;
            lblPolicyRecoveryModel.Text = "Recovery Model: --";
            // 
            // lblPolicyRetention
            // 
            lblPolicyRetention.AutoSize = true;
            lblPolicyRetention.Location = new Point(10, 131);
            lblPolicyRetention.Name = "lblPolicyRetention";
            lblPolicyRetention.Size = new Size(74, 15);
            lblPolicyRetention.TabIndex = 5;
            lblPolicyRetention.Text = "Retention: --";
            // 
            // lblPolicyBootstrap
            // 
            lblPolicyBootstrap.Location = new Point(10, 149);
            lblPolicyBootstrap.Name = "lblPolicyBootstrap";
            lblPolicyBootstrap.Size = new Size(620, 44);
            lblPolicyBootstrap.TabIndex = 6;
            lblPolicyBootstrap.Text = "";
            // 
            // panelDatabaseTopology
            // 
            panelDatabaseTopology.BackColor = Color.White;
            panelDatabaseTopology.BorderStyle = BorderStyle.FixedSingle;
            panelDatabaseTopology.Controls.Add(lblDatabaseTopologyTitle);
            panelDatabaseTopology.Controls.Add(lblDbName);
            panelDatabaseTopology.Controls.Add(lblDbServer);
            panelDatabaseTopology.Controls.Add(lblDbRecoveryModel);
            panelDatabaseTopology.Controls.Add(lblTopologyProdServer);
            panelDatabaseTopology.Controls.Add(lblTopologyBackupServer);
            panelDatabaseTopology.Controls.Add(lblTopologyDestinationPath);
            panelDatabaseTopology.Controls.Add(lblPulseStatus);
            panelDatabaseTopology.Controls.Add(lblPulseLastChecked);
            panelDatabaseTopology.Controls.Add(lblChainInitialized);
            panelDatabaseTopology.Controls.Add(lblLastValidFullBackup);
            panelDatabaseTopology.Controls.Add(lblRestoreChainHealthSimple);
            panelDatabaseTopology.Controls.Add(lblChainInitializationWarning);
            panelDatabaseTopology.Dock = DockStyle.Fill;
            panelDatabaseTopology.Location = new Point(658, 3);
            panelDatabaseTopology.Name = "panelDatabaseTopology";
            panelDatabaseTopology.Padding = new Padding(10);
            panelDatabaseTopology.Size = new Size(795, 232);
            panelDatabaseTopology.TabIndex = 1;
            // 
            // lblDatabaseTopologyTitle
            // 
            lblDatabaseTopologyTitle.AutoSize = true;
            lblDatabaseTopologyTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblDatabaseTopologyTitle.Location = new Point(10, 8);
            lblDatabaseTopologyTitle.Name = "lblDatabaseTopologyTitle";
            lblDatabaseTopologyTitle.Size = new Size(118, 20);
            lblDatabaseTopologyTitle.TabIndex = 0;
            lblDatabaseTopologyTitle.Text = "System Context";
            // 
            // lblDbName
            // 
            lblDbName.AutoSize = true;
            lblDbName.Location = new Point(10, 34);
            lblDbName.Name = "lblDbName";
            lblDbName.Size = new Size(71, 15);
            lblDbName.TabIndex = 1;
            lblDbName.Text = "Database: --";
            // 
            // lblDbServer
            // 
            lblDbServer.AutoSize = true;
            lblDbServer.Location = new Point(10, 51);
            lblDbServer.Name = "lblDbServer";
            lblDbServer.Size = new Size(141, 15);
            lblDbServer.TabIndex = 2;
            lblDbServer.Text = "Production SQL Server: --";
            // 
            // lblDbRecoveryModel
            // 
            lblDbRecoveryModel.AutoSize = true;
            lblDbRecoveryModel.Location = new Point(10, 68);
            lblDbRecoveryModel.Name = "lblDbRecoveryModel";
            lblDbRecoveryModel.Size = new Size(108, 15);
            lblDbRecoveryModel.TabIndex = 3;
            lblDbRecoveryModel.Text = "Recovery Model: --";
            // 
            // lblTopologyProdServer
            // 
            lblTopologyProdServer.AutoSize = true;
            lblTopologyProdServer.Location = new Point(10, 86);
            lblTopologyProdServer.Name = "lblTopologyProdServer";
            lblTopologyProdServer.Size = new Size(135, 15);
            lblTopologyProdServer.TabIndex = 4;
            lblTopologyProdServer.Text = "Production DB Server: --";
            // 
            // lblTopologyBackupServer
            // 
            lblTopologyBackupServer.AutoSize = true;
            lblTopologyBackupServer.Location = new Point(10, 103);
            lblTopologyBackupServer.Name = "lblTopologyBackupServer";
            lblTopologyBackupServer.Size = new Size(140, 15);
            lblTopologyBackupServer.TabIndex = 5;
            lblTopologyBackupServer.Text = "Backup Storage Server: --";
            // 
            // lblTopologyDestinationPath
            // 
            lblTopologyDestinationPath.Location = new Point(10, 120);
            lblTopologyDestinationPath.Name = "lblTopologyDestinationPath";
            lblTopologyDestinationPath.Size = new Size(770, 16);
            lblTopologyDestinationPath.TabIndex = 6;
            lblTopologyDestinationPath.Text = "Backup Destination: --";
            // 
            // lblPulseStatus
            // 
            lblPulseStatus.AutoSize = true;
            lblPulseStatus.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblPulseStatus.Location = new Point(10, 142);
            lblPulseStatus.Name = "lblPulseStatus";
            lblPulseStatus.Size = new Size(119, 19);
            lblPulseStatus.TabIndex = 7;
            lblPulseStatus.Text = "Status: Unknown";
            // 
            // lblPulseLastChecked
            // 
            lblPulseLastChecked.AutoSize = true;
            lblPulseLastChecked.Location = new Point(10, 162);
            lblPulseLastChecked.Name = "lblPulseLastChecked";
            lblPulseLastChecked.Size = new Size(93, 15);
            lblPulseLastChecked.TabIndex = 8;
            lblPulseLastChecked.Text = "Last Checked: --";
            // 
            // lblChainInitialized
            // 
            lblChainInitialized.AutoSize = true;
            lblChainInitialized.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblChainInitialized.Location = new Point(246, 142);
            lblChainInitialized.Name = "lblChainInitialized";
            lblChainInitialized.Size = new Size(188, 19);
            lblChainInitialized.TabIndex = 9;
            lblChainInitialized.Text = "Backup Chain Initialized: --";
            // 
            // lblLastValidFullBackup
            // 
            lblLastValidFullBackup.Location = new Point(246, 162);
            lblLastValidFullBackup.Name = "lblLastValidFullBackup";
            lblLastValidFullBackup.Size = new Size(534, 16);
            lblLastValidFullBackup.TabIndex = 10;
            lblLastValidFullBackup.Text = "Last Valid Full Backup: --";
            // 
            // lblRestoreChainHealthSimple
            // 
            lblRestoreChainHealthSimple.AutoSize = true;
            lblRestoreChainHealthSimple.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblRestoreChainHealthSimple.Location = new Point(246, 180);
            lblRestoreChainHealthSimple.Name = "lblRestoreChainHealthSimple";
            lblRestoreChainHealthSimple.Size = new Size(169, 19);
            lblRestoreChainHealthSimple.TabIndex = 11;
            lblRestoreChainHealthSimple.Text = "Restore Chain Health: --";
            // 
            // lblChainInitializationWarning
            // 
            lblChainInitializationWarning.Location = new Point(246, 200);
            lblChainInitializationWarning.Name = "lblChainInitializationWarning";
            lblChainInitializationWarning.Size = new Size(534, 22);
            lblChainInitializationWarning.TabIndex = 12;
            lblChainInitializationWarning.Text = "--";
            // 
            // MonitoringDashboard
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(243, 246, 250);
            ClientSize = new Size(1280, 720);
            Controls.Add(tableBody);
            Controls.Add(panelDetails);
            Controls.Add(panelHeader);
            MinimumSize = new Size(1200, 700);
            Name = "MonitoringDashboard";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Deadpool - Backup Monitoring Dashboard";
            Load += MonitoringDashboard_Load;
            panelHeader.ResumeLayout(false);
            panelHeader.PerformLayout();
            tableBody.ResumeLayout(false);
            tableLeftCards.ResumeLayout(false);
            tableMainBottom.ResumeLayout(false);
            panelLastBackupStatus.ResumeLayout(false);
            panelLastBackupStatus.PerformLayout();
            panelChainHealthCard.ResumeLayout(false);
            panelChainHealthCard.PerformLayout();
            panelStorageStatus.ResumeLayout(false);
            panelStorageStatus.PerformLayout();
            panelDatabaseCard.ResumeLayout(false);
            panelDatabaseCard.PerformLayout();
            panelAlert.ResumeLayout(false);
            panelAlert.PerformLayout();
            panelRecentJobs.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvRecentJobs).EndInit();
            panelDetails.ResumeLayout(false);
            tableDetails.ResumeLayout(false);
            panelBackupPolicy.ResumeLayout(false);
            panelBackupPolicy.PerformLayout();
            panelDatabaseTopology.ResumeLayout(false);
            panelDatabaseTopology.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Panel panelHeader;
        private Label lblTitle;
        private Label lblSystemStatus;
        private Label lblDbSummary;
        private Label lblNextRisk;
        private Label lblLastRefresh;
        private Button btnJobMonitor;
        private Button btnRefresh;
        private TableLayoutPanel tableBody;
        private TableLayoutPanel tableLeftCards;
        private TableLayoutPanel tableMainBottom;
        private Panel panelLastBackupStatus;
        private Label lblLastBackupTitle;
        private Label lblFullBackup;
        private Label lblDiffBackup;
        private Label lblLogBackup;
        private Panel panelChainHealthCard;
        private Label lblChainHealthTitle;
        private Label lblChainHealth;
        private Label lblChainMessage;
        private Label lblChainAction;
        private Panel panelStorageStatus;
        private Label lblStorageTitle;
        private Label lblStoragePath;
        private Label lblStorageSpace;
        private Label lblEstimatedFullBackup;
        private Label lblStoragePrediction;
        private ProgressBar progressBarStorage;
        private Panel panelDatabaseCard;
        private Label lblDatabaseCardTitle;
        private Label lblDatabaseStatus;
        private Panel panelAlert;
        private Label lblAlertTitle;
        private Label lblAlertMessage;
        private Label lblAlertAction;
        private Panel panelRecentJobs;
        private Label lblRecentJobsTitle;
        private DataGridView dgvRecentJobs;
        private Panel panelDetails;
        private TableLayoutPanel tableDetails;
        private Panel panelBackupPolicy;
        private Label lblPolicyTitle;
        private Label lblPolicyFullSchedule;
        private Label lblPolicyDifferentialSchedule;
        private Label lblPolicyLogSchedule;
        private Label lblPolicyRecoveryModel;
        private Label lblPolicyRetention;
        private Label lblPolicyBootstrap;
        private Panel panelDatabaseTopology;
        private Label lblDatabaseTopologyTitle;
        private Label lblDbName;
        private Label lblDbServer;
        private Label lblDbRecoveryModel;
        private Label lblTopologyProdServer;
        private Label lblTopologyBackupServer;
        private Label lblTopologyDestinationPath;
        private Label lblPulseStatus;
        private Label lblPulseLastChecked;
        private Label lblChainInitialized;
        private Label lblLastValidFullBackup;
        private Label lblRestoreChainHealthSimple;
        private Label lblChainInitializationWarning;
    }
}
