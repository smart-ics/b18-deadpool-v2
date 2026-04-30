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
            this.panelHeader = new System.Windows.Forms.Panel();
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblLastRefresh = new System.Windows.Forms.Label();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.btnJobMonitor = new System.Windows.Forms.Button();
            this.panelLastBackupStatus = new System.Windows.Forms.Panel();
            this.lblLastBackupTitle = new System.Windows.Forms.Label();
            this.lblFullBackup = new System.Windows.Forms.Label();
            this.lblDiffBackup = new System.Windows.Forms.Label();
            this.lblLogBackup = new System.Windows.Forms.Label();
            this.lblChainHealth = new System.Windows.Forms.Label();
            this.lstWarnings = new System.Windows.Forms.ListBox();
            this.panelRecentJobs = new System.Windows.Forms.Panel();
            this.lblRecentJobsTitle = new System.Windows.Forms.Label();
            this.dgvRecentJobs = new System.Windows.Forms.DataGridView();
            this.panelStorageStatus = new System.Windows.Forms.Panel();
            this.lblStorageTitle = new System.Windows.Forms.Label();
            this.lblStoragePath = new System.Windows.Forms.Label();
            this.lblStorageSpace = new System.Windows.Forms.Label();
            this.lblStorageHealth = new System.Windows.Forms.Label();
            this.progressBarStorage = new System.Windows.Forms.ProgressBar();
            this.panelBackupPolicy = new System.Windows.Forms.Panel();
            this.lblPolicyTitle = new System.Windows.Forms.Label();
            this.lblPolicyFullSchedule = new System.Windows.Forms.Label();
            this.lblPolicyDifferentialSchedule = new System.Windows.Forms.Label();
            this.lblPolicyLogSchedule = new System.Windows.Forms.Label();
            this.lblPolicyRecoveryModel = new System.Windows.Forms.Label();
            this.lblPolicyRetention = new System.Windows.Forms.Label();
            this.lblPolicyBootstrap = new System.Windows.Forms.Label();
            this.panelDatabaseTopology = new System.Windows.Forms.Panel();
            this.lblDatabaseTopologyTitle = new System.Windows.Forms.Label();
            this.lblDbName = new System.Windows.Forms.Label();
            this.lblDbServer = new System.Windows.Forms.Label();
            this.lblDbRecoveryModel = new System.Windows.Forms.Label();
            this.lblTopologyProdServer = new System.Windows.Forms.Label();
            this.lblTopologyBackupServer = new System.Windows.Forms.Label();
            this.lblTopologyDestinationPath = new System.Windows.Forms.Label();
            this.lblPulseStatus = new System.Windows.Forms.Label();
            this.lblPulseLastChecked = new System.Windows.Forms.Label();
            this.panelHeader.SuspendLayout();
            this.panelLastBackupStatus.SuspendLayout();
            this.panelRecentJobs.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRecentJobs)).BeginInit();
            this.panelStorageStatus.SuspendLayout();
            this.panelBackupPolicy.SuspendLayout();
            this.panelDatabaseTopology.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelHeader
            // 
            this.panelHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
            this.panelHeader.Controls.Add(this.lblTitle);
            this.panelHeader.Controls.Add(this.lblLastRefresh);
            this.panelHeader.Controls.Add(this.btnRefresh);
            this.panelHeader.Controls.Add(this.btnJobMonitor);
            this.panelHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelHeader.Location = new System.Drawing.Point(0, 0);
            this.panelHeader.Name = "panelHeader";
            this.panelHeader.Size = new System.Drawing.Size(1200, 60);
            this.panelHeader.TabIndex = 0;
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblTitle.ForeColor = System.Drawing.Color.White;
            this.lblTitle.Location = new System.Drawing.Point(12, 15);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(310, 30);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Backup Monitoring Dashboard";
            // 
            // lblLastRefresh
            // 
            this.lblLastRefresh.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblLastRefresh.ForeColor = System.Drawing.Color.LightGray;
            this.lblLastRefresh.Location = new System.Drawing.Point(850, 20);
            this.lblLastRefresh.Name = "lblLastRefresh";
            this.lblLastRefresh.Size = new System.Drawing.Size(220, 20);
            this.lblLastRefresh.TabIndex = 1;
            this.lblLastRefresh.Text = "Last refresh: --";
            this.lblLastRefresh.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // btnRefresh
            // 
            this.btnRefresh.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRefresh.Location = new System.Drawing.Point(1080, 15);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(100, 30);
            this.btnRefresh.TabIndex = 2;
            this.btnRefresh.Text = "Refresh";
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            // 
            // btnJobMonitor
            // 
            this.btnJobMonitor.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnJobMonitor.Location = new System.Drawing.Point(950, 15);
            this.btnJobMonitor.Name = "btnJobMonitor";
            this.btnJobMonitor.Size = new System.Drawing.Size(120, 30);
            this.btnJobMonitor.TabIndex = 3;
            this.btnJobMonitor.Text = "Job Monitor";
            this.btnJobMonitor.UseVisualStyleBackColor = true;
            this.btnJobMonitor.Click += new System.EventHandler(this.btnJobMonitor_Click);
            // 
            // panelLastBackupStatus
            // 
            this.panelLastBackupStatus.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelLastBackupStatus.Controls.Add(this.lblLastBackupTitle);
            this.panelLastBackupStatus.Controls.Add(this.lblFullBackup);
            this.panelLastBackupStatus.Controls.Add(this.lblDiffBackup);
            this.panelLastBackupStatus.Controls.Add(this.lblLogBackup);
            this.panelLastBackupStatus.Controls.Add(this.lblChainHealth);
            this.panelLastBackupStatus.Controls.Add(this.lstWarnings);
            this.panelLastBackupStatus.Location = new System.Drawing.Point(12, 70);
            this.panelLastBackupStatus.Name = "panelLastBackupStatus";
            this.panelLastBackupStatus.Size = new System.Drawing.Size(380, 250);
            this.panelLastBackupStatus.TabIndex = 1;
            // 
            // lblLastBackupTitle
            // 
            this.lblLastBackupTitle.AutoSize = true;
            this.lblLastBackupTitle.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.lblLastBackupTitle.Location = new System.Drawing.Point(10, 10);
            this.lblLastBackupTitle.Name = "lblLastBackupTitle";
            this.lblLastBackupTitle.Size = new System.Drawing.Size(141, 20);
            this.lblLastBackupTitle.TabIndex = 0;
            this.lblLastBackupTitle.Text = "Last Backup Status";
            // 
            // lblFullBackup
            // 
            this.lblFullBackup.AutoSize = true;
            this.lblFullBackup.Location = new System.Drawing.Point(10, 40);
            this.lblFullBackup.Name = "lblFullBackup";
            this.lblFullBackup.Size = new System.Drawing.Size(100, 15);
            this.lblFullBackup.TabIndex = 1;
            this.lblFullBackup.Text = "Full: --";
            // 
            // lblDiffBackup
            // 
            this.lblDiffBackup.AutoSize = true;
            this.lblDiffBackup.Location = new System.Drawing.Point(10, 60);
            this.lblDiffBackup.Name = "lblDiffBackup";
            this.lblDiffBackup.Size = new System.Drawing.Size(100, 15);
            this.lblDiffBackup.TabIndex = 2;
            this.lblDiffBackup.Text = "Differential: --";
            // 
            // lblLogBackup
            // 
            this.lblLogBackup.AutoSize = true;
            this.lblLogBackup.Location = new System.Drawing.Point(10, 80);
            this.lblLogBackup.Name = "lblLogBackup";
            this.lblLogBackup.Size = new System.Drawing.Size(100, 15);
            this.lblLogBackup.TabIndex = 3;
            this.lblLogBackup.Text = "Log: --";
            // 
            // lblChainHealth
            // 
            this.lblChainHealth.AutoSize = true;
            this.lblChainHealth.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblChainHealth.Location = new System.Drawing.Point(10, 105);
            this.lblChainHealth.Name = "lblChainHealth";
            this.lblChainHealth.Size = new System.Drawing.Size(150, 19);
            this.lblChainHealth.TabIndex = 4;
            this.lblChainHealth.Text = "Chain Health: Unknown";
            // 
            // lstWarnings
            // 
            this.lstWarnings.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lstWarnings.FormattingEnabled = true;
            this.lstWarnings.ItemHeight = 15;
            this.lstWarnings.Location = new System.Drawing.Point(10, 135);
            this.lstWarnings.Name = "lstWarnings";
            this.lstWarnings.Size = new System.Drawing.Size(358, 154);
            this.lstWarnings.TabIndex = 5;
            // 
            // panelRecentJobs
            // 
            this.panelRecentJobs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelRecentJobs.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelRecentJobs.Controls.Add(this.lblRecentJobsTitle);
            this.panelRecentJobs.Controls.Add(this.dgvRecentJobs);
            this.panelRecentJobs.Location = new System.Drawing.Point(400, 70);
            this.panelRecentJobs.Name = "panelRecentJobs";
            this.panelRecentJobs.Size = new System.Drawing.Size(788, 598);
            this.panelRecentJobs.TabIndex = 2;
            // 
            // lblRecentJobsTitle
            // 
            this.lblRecentJobsTitle.AutoSize = true;
            this.lblRecentJobsTitle.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.lblRecentJobsTitle.Location = new System.Drawing.Point(10, 10);
            this.lblRecentJobsTitle.Name = "lblRecentJobsTitle";
            this.lblRecentJobsTitle.Size = new System.Drawing.Size(91, 20);
            this.lblRecentJobsTitle.TabIndex = 0;
            this.lblRecentJobsTitle.Text = "Recent Jobs";
            // 
            // dgvRecentJobs
            // 
            this.dgvRecentJobs.AllowUserToAddRows = false;
            this.dgvRecentJobs.AllowUserToDeleteRows = false;
            this.dgvRecentJobs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvRecentJobs.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells;
            this.dgvRecentJobs.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvRecentJobs.Location = new System.Drawing.Point(10, 40);
            this.dgvRecentJobs.Name = "dgvRecentJobs";
            this.dgvRecentJobs.ReadOnly = true;
            this.dgvRecentJobs.RowHeadersVisible = false;
            this.dgvRecentJobs.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvRecentJobs.Size = new System.Drawing.Size(768, 546);
            this.dgvRecentJobs.TabIndex = 1;
            // 
            // panelStorageStatus
            // 
            this.panelStorageStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)));
            this.panelStorageStatus.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelStorageStatus.Controls.Add(this.lblStorageTitle);
            this.panelStorageStatus.Controls.Add(this.lblStoragePath);
            this.panelStorageStatus.Controls.Add(this.lblStorageSpace);
            this.panelStorageStatus.Controls.Add(this.lblStorageHealth);
            this.panelStorageStatus.Controls.Add(this.progressBarStorage);
            this.panelStorageStatus.Location = new System.Drawing.Point(12, 716);
            this.panelStorageStatus.Name = "panelStorageStatus";
            this.panelStorageStatus.Size = new System.Drawing.Size(380, 148);
            this.panelStorageStatus.TabIndex = 3;
            // 
            // lblStorageTitle
            // 
            this.lblStorageTitle.AutoSize = true;
            this.lblStorageTitle.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.lblStorageTitle.Location = new System.Drawing.Point(10, 10);
            this.lblStorageTitle.Name = "lblStorageTitle";
            this.lblStorageTitle.Size = new System.Drawing.Size(109, 20);
            this.lblStorageTitle.TabIndex = 0;
            this.lblStorageTitle.Text = "Storage Status";
            // 
            // lblStoragePath
            // 
            this.lblStoragePath.AutoSize = true;
            this.lblStoragePath.Location = new System.Drawing.Point(10, 40);
            this.lblStoragePath.Name = "lblStoragePath";
            this.lblStoragePath.Size = new System.Drawing.Size(100, 15);
            this.lblStoragePath.TabIndex = 1;
            this.lblStoragePath.Text = "Path: --";
            // 
            // lblStorageSpace
            // 
            this.lblStorageSpace.AutoSize = true;
            this.lblStorageSpace.Location = new System.Drawing.Point(10, 86);
            this.lblStorageSpace.Name = "lblStorageSpace";
            this.lblStorageSpace.Size = new System.Drawing.Size(150, 15);
            this.lblStorageSpace.TabIndex = 2;
            this.lblStorageSpace.Text = "Free: -- / -- (--%)";
            // 
            // lblStorageHealth
            // 
            this.lblStorageHealth.AutoSize = true;
            this.lblStorageHealth.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblStorageHealth.Location = new System.Drawing.Point(10, 112);
            this.lblStorageHealth.Name = "lblStorageHealth";
            this.lblStorageHealth.Size = new System.Drawing.Size(130, 19);
            this.lblStorageHealth.TabIndex = 3;
            this.lblStorageHealth.Text = "Health: Unknown";
            // 
            // progressBarStorage
            // 
            this.progressBarStorage.Location = new System.Drawing.Point(10, 60);
            this.progressBarStorage.Name = "progressBarStorage";
            this.progressBarStorage.Size = new System.Drawing.Size(358, 23);
            this.progressBarStorage.TabIndex = 4;
            // 
            // panelBackupPolicy
            // 
            this.panelBackupPolicy.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelBackupPolicy.Controls.Add(this.lblPolicyTitle);
            this.panelBackupPolicy.Controls.Add(this.lblPolicyFullSchedule);
            this.panelBackupPolicy.Controls.Add(this.lblPolicyDifferentialSchedule);
            this.panelBackupPolicy.Controls.Add(this.lblPolicyLogSchedule);
            this.panelBackupPolicy.Controls.Add(this.lblPolicyRecoveryModel);
            this.panelBackupPolicy.Controls.Add(this.lblPolicyRetention);
            this.panelBackupPolicy.Controls.Add(this.lblPolicyBootstrap);
            this.panelBackupPolicy.Location = new System.Drawing.Point(12, 330);
            this.panelBackupPolicy.Name = "panelBackupPolicy";
            this.panelBackupPolicy.Size = new System.Drawing.Size(380, 180);
            this.panelBackupPolicy.TabIndex = 4;
            // 
            // lblPolicyTitle
            // 
            this.lblPolicyTitle.AutoSize = true;
            this.lblPolicyTitle.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.lblPolicyTitle.Location = new System.Drawing.Point(10, 10);
            this.lblPolicyTitle.Name = "lblPolicyTitle";
            this.lblPolicyTitle.Size = new System.Drawing.Size(98, 20);
            this.lblPolicyTitle.TabIndex = 0;
            this.lblPolicyTitle.Text = "Backup Policy";
            // 
            // lblPolicyFullSchedule
            // 
            this.lblPolicyFullSchedule.Location = new System.Drawing.Point(10, 38);
            this.lblPolicyFullSchedule.Name = "lblPolicyFullSchedule";
            this.lblPolicyFullSchedule.Size = new System.Drawing.Size(358, 26);
            this.lblPolicyFullSchedule.TabIndex = 1;
            this.lblPolicyFullSchedule.Text = "Full Backup runs --";
            // 
            // lblPolicyDifferentialSchedule
            // 
            this.lblPolicyDifferentialSchedule.Location = new System.Drawing.Point(10, 62);
            this.lblPolicyDifferentialSchedule.Name = "lblPolicyDifferentialSchedule";
            this.lblPolicyDifferentialSchedule.Size = new System.Drawing.Size(358, 26);
            this.lblPolicyDifferentialSchedule.TabIndex = 2;
            this.lblPolicyDifferentialSchedule.Text = "Differential Backup runs --";
            // 
            // lblPolicyLogSchedule
            // 
            this.lblPolicyLogSchedule.Location = new System.Drawing.Point(10, 86);
            this.lblPolicyLogSchedule.Name = "lblPolicyLogSchedule";
            this.lblPolicyLogSchedule.Size = new System.Drawing.Size(358, 26);
            this.lblPolicyLogSchedule.TabIndex = 3;
            this.lblPolicyLogSchedule.Text = "Transaction Log Backup runs --";
            // 
            // lblPolicyRecoveryModel
            // 
            this.lblPolicyRecoveryModel.AutoSize = true;
            this.lblPolicyRecoveryModel.Location = new System.Drawing.Point(10, 114);
            this.lblPolicyRecoveryModel.Name = "lblPolicyRecoveryModel";
            this.lblPolicyRecoveryModel.Size = new System.Drawing.Size(101, 15);
            this.lblPolicyRecoveryModel.TabIndex = 4;
            this.lblPolicyRecoveryModel.Text = "Recovery Model: --";
            // 
            // lblPolicyRetention
            // 
            this.lblPolicyRetention.AutoSize = true;
            this.lblPolicyRetention.Location = new System.Drawing.Point(10, 132);
            this.lblPolicyRetention.Name = "lblPolicyRetention";
            this.lblPolicyRetention.Size = new System.Drawing.Size(77, 15);
            this.lblPolicyRetention.TabIndex = 5;
            this.lblPolicyRetention.Text = "Retention: --";
            // 
            // lblPolicyBootstrap
            // 
            this.lblPolicyBootstrap.AutoSize = true;
            this.lblPolicyBootstrap.Location = new System.Drawing.Point(10, 150);
            this.lblPolicyBootstrap.Name = "lblPolicyBootstrap";
            this.lblPolicyBootstrap.Size = new System.Drawing.Size(0, 15);
            this.lblPolicyBootstrap.TabIndex = 6;
            // 
            // panelDatabaseTopology
            // 
            this.panelDatabaseTopology.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelDatabaseTopology.Controls.Add(this.lblDatabaseTopologyTitle);
            this.panelDatabaseTopology.Controls.Add(this.lblDbName);
            this.panelDatabaseTopology.Controls.Add(this.lblDbServer);
            this.panelDatabaseTopology.Controls.Add(this.lblDbRecoveryModel);
            this.panelDatabaseTopology.Controls.Add(this.lblTopologyProdServer);
            this.panelDatabaseTopology.Controls.Add(this.lblTopologyBackupServer);
            this.panelDatabaseTopology.Controls.Add(this.lblTopologyDestinationPath);
            this.panelDatabaseTopology.Controls.Add(this.lblPulseStatus);
            this.panelDatabaseTopology.Controls.Add(this.lblPulseLastChecked);
            this.panelDatabaseTopology.Location = new System.Drawing.Point(12, 520);
            this.panelDatabaseTopology.Name = "panelDatabaseTopology";
            this.panelDatabaseTopology.Size = new System.Drawing.Size(380, 190);
            this.panelDatabaseTopology.TabIndex = 5;
            // 
            // lblDatabaseTopologyTitle
            // 
            this.lblDatabaseTopologyTitle.AutoSize = true;
            this.lblDatabaseTopologyTitle.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.lblDatabaseTopologyTitle.Location = new System.Drawing.Point(10, 10);
            this.lblDatabaseTopologyTitle.Name = "lblDatabaseTopologyTitle";
            this.lblDatabaseTopologyTitle.Size = new System.Drawing.Size(129, 20);
            this.lblDatabaseTopologyTitle.TabIndex = 0;
            this.lblDatabaseTopologyTitle.Text = "Database Topology";
            // 
            // lblDbName
            // 
            this.lblDbName.AutoSize = true;
            this.lblDbName.Location = new System.Drawing.Point(10, 36);
            this.lblDbName.Name = "lblDbName";
            this.lblDbName.Size = new System.Drawing.Size(78, 15);
            this.lblDbName.TabIndex = 1;
            this.lblDbName.Text = "Database: --";
            // 
            // lblDbServer
            // 
            this.lblDbServer.AutoSize = true;
            this.lblDbServer.Location = new System.Drawing.Point(10, 54);
            this.lblDbServer.Name = "lblDbServer";
            this.lblDbServer.Size = new System.Drawing.Size(151, 15);
            this.lblDbServer.TabIndex = 2;
            this.lblDbServer.Text = "Production SQL Server: --";
            // 
            // lblDbRecoveryModel
            // 
            this.lblDbRecoveryModel.AutoSize = true;
            this.lblDbRecoveryModel.Location = new System.Drawing.Point(10, 72);
            this.lblDbRecoveryModel.Name = "lblDbRecoveryModel";
            this.lblDbRecoveryModel.Size = new System.Drawing.Size(117, 15);
            this.lblDbRecoveryModel.TabIndex = 3;
            this.lblDbRecoveryModel.Text = "Recovery Model: --";
            // 
            // lblTopologyProdServer
            // 
            this.lblTopologyProdServer.AutoSize = true;
            this.lblTopologyProdServer.Location = new System.Drawing.Point(10, 96);
            this.lblTopologyProdServer.Name = "lblTopologyProdServer";
            this.lblTopologyProdServer.Size = new System.Drawing.Size(143, 15);
            this.lblTopologyProdServer.TabIndex = 4;
            this.lblTopologyProdServer.Text = "Production DB Server: --";
            // 
            // lblTopologyBackupServer
            // 
            this.lblTopologyBackupServer.AutoSize = true;
            this.lblTopologyBackupServer.Location = new System.Drawing.Point(10, 114);
            this.lblTopologyBackupServer.Name = "lblTopologyBackupServer";
            this.lblTopologyBackupServer.Size = new System.Drawing.Size(145, 15);
            this.lblTopologyBackupServer.TabIndex = 5;
            this.lblTopologyBackupServer.Text = "Backup Storage Server: --";
            // 
            // lblTopologyDestinationPath
            // 
            this.lblTopologyDestinationPath.AutoSize = true;
            this.lblTopologyDestinationPath.Location = new System.Drawing.Point(10, 132);
            this.lblTopologyDestinationPath.Name = "lblTopologyDestinationPath";
            this.lblTopologyDestinationPath.Size = new System.Drawing.Size(131, 15);
            this.lblTopologyDestinationPath.TabIndex = 6;
            this.lblTopologyDestinationPath.Text = "Backup Destination: --";
            // 
            // lblPulseStatus
            // 
            this.lblPulseStatus.AutoSize = true;
            this.lblPulseStatus.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblPulseStatus.Location = new System.Drawing.Point(10, 151);
            this.lblPulseStatus.Name = "lblPulseStatus";
            this.lblPulseStatus.Size = new System.Drawing.Size(107, 19);
            this.lblPulseStatus.TabIndex = 7;
            this.lblPulseStatus.Text = "Status: Unknown";
            // 
            // lblPulseLastChecked
            // 
            this.lblPulseLastChecked.AutoSize = true;
            this.lblPulseLastChecked.Location = new System.Drawing.Point(10, 170);
            this.lblPulseLastChecked.Name = "lblPulseLastChecked";
            this.lblPulseLastChecked.Size = new System.Drawing.Size(95, 15);
            this.lblPulseLastChecked.TabIndex = 8;
            this.lblPulseLastChecked.Text = "Last Checked: --";
            // 
            // MonitoringDashboard
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1200, 880);
            this.Controls.Add(this.panelDatabaseTopology);
            this.Controls.Add(this.panelBackupPolicy);
            this.Controls.Add(this.panelStorageStatus);
            this.Controls.Add(this.panelRecentJobs);
            this.Controls.Add(this.panelLastBackupStatus);
            this.Controls.Add(this.panelHeader);
            this.MinimumSize = new System.Drawing.Size(1000, 880);
            this.Name = "MonitoringDashboard";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Deadpool - Backup Monitoring Dashboard";
            this.Load += new System.EventHandler(this.MonitoringDashboard_Load);
            this.panelHeader.ResumeLayout(false);
            this.panelHeader.PerformLayout();
            this.panelLastBackupStatus.ResumeLayout(false);
            this.panelLastBackupStatus.PerformLayout();
            this.panelRecentJobs.ResumeLayout(false);
            this.panelRecentJobs.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRecentJobs)).EndInit();
            this.panelStorageStatus.ResumeLayout(false);
            this.panelStorageStatus.PerformLayout();
            this.panelBackupPolicy.ResumeLayout(false);
            this.panelBackupPolicy.PerformLayout();
            this.panelDatabaseTopology.ResumeLayout(false);
            this.panelDatabaseTopology.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelHeader;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblLastRefresh;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Button btnJobMonitor;
        private System.Windows.Forms.Panel panelLastBackupStatus;
        private System.Windows.Forms.Label lblLastBackupTitle;
        private System.Windows.Forms.Label lblFullBackup;
        private System.Windows.Forms.Label lblDiffBackup;
        private System.Windows.Forms.Label lblLogBackup;
        private System.Windows.Forms.Label lblChainHealth;
        private System.Windows.Forms.ListBox lstWarnings;
        private System.Windows.Forms.Panel panelRecentJobs;
        private System.Windows.Forms.Label lblRecentJobsTitle;
        private System.Windows.Forms.DataGridView dgvRecentJobs;
        private System.Windows.Forms.Panel panelStorageStatus;
        private System.Windows.Forms.Label lblStorageTitle;
        private System.Windows.Forms.Label lblStoragePath;
        private System.Windows.Forms.Label lblStorageSpace;
        private System.Windows.Forms.Label lblStorageHealth;
        private System.Windows.Forms.ProgressBar progressBarStorage;
        private System.Windows.Forms.Panel panelBackupPolicy;
        private System.Windows.Forms.Label lblPolicyTitle;
        private System.Windows.Forms.Label lblPolicyFullSchedule;
        private System.Windows.Forms.Label lblPolicyDifferentialSchedule;
        private System.Windows.Forms.Label lblPolicyLogSchedule;
        private System.Windows.Forms.Label lblPolicyRecoveryModel;
        private System.Windows.Forms.Label lblPolicyRetention;
        private System.Windows.Forms.Label lblPolicyBootstrap;
        private System.Windows.Forms.Panel panelDatabaseTopology;
        private System.Windows.Forms.Label lblDatabaseTopologyTitle;
        private System.Windows.Forms.Label lblDbName;
        private System.Windows.Forms.Label lblDbServer;
        private System.Windows.Forms.Label lblDbRecoveryModel;
        private System.Windows.Forms.Label lblTopologyProdServer;
        private System.Windows.Forms.Label lblTopologyBackupServer;
        private System.Windows.Forms.Label lblTopologyDestinationPath;
        private System.Windows.Forms.Label lblPulseStatus;
        private System.Windows.Forms.Label lblPulseLastChecked;
    }
}
