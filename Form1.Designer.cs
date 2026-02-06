namespace InstanceManager
{
    partial class Main
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.HeaderPanel = new System.Windows.Forms.Panel();
            this.SubtitleLabel = new System.Windows.Forms.Label();
            this.TitleLabel = new System.Windows.Forms.Label();
            this.LogoPictureBox = new System.Windows.Forms.PictureBox();
            this.MainSplitContainer = new System.Windows.Forms.SplitContainer();
            // Left panel controls
            this.GroupLabel = new System.Windows.Forms.Label();
            this.GroupListBox = new System.Windows.Forms.ListBox();
            this.GroupButtonPanel = new System.Windows.Forms.Panel();
            this.AddGroupButton = new System.Windows.Forms.Button();
            this.EditGroupButton = new System.Windows.Forms.Button();
            this.DeleteGroupButton = new System.Windows.Forms.Button();
            // Right panel controls
            this.SelectedGroupLabel = new System.Windows.Forms.Label();
            this.AppListView = new System.Windows.Forms.ListView();
            this.IndexColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.AppNameColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.DirectoryColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.StatusColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.KeepOpenColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.CrashCountColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.RetryCountColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.LastStartColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.LastStopColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ButtonPanel = new System.Windows.Forms.Panel();
            this.RefreshButton = new System.Windows.Forms.Button();
            this.StopAllButton = new System.Windows.Forms.Button();
            this.StartAllButton = new System.Windows.Forms.Button();
            this.StopButton = new System.Windows.Forms.Button();
            this.StartButton = new System.Windows.Forms.Button();
            this.DeleteButton = new System.Windows.Forms.Button();
            this.EditButton = new System.Windows.Forms.Button();
            this.AddButton = new System.Windows.Forms.Button();
            this.LogsButton = new System.Windows.Forms.Button();
            this.HeaderPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.LogoPictureBox)).BeginInit();
            this.MainSplitContainer.Panel1.SuspendLayout();
            this.MainSplitContainer.Panel2.SuspendLayout();
            this.MainSplitContainer.SuspendLayout();
            this.GroupButtonPanel.SuspendLayout();
            this.ButtonPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // HeaderPanel
            // 
            this.HeaderPanel.BackColor = System.Drawing.SystemColors.Control;
            this.HeaderPanel.Controls.Add(this.SubtitleLabel);
            this.HeaderPanel.Controls.Add(this.TitleLabel);
            this.HeaderPanel.Controls.Add(this.LogoPictureBox);
            this.HeaderPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.HeaderPanel.Location = new System.Drawing.Point(0, 0);
            this.HeaderPanel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.HeaderPanel.Name = "HeaderPanel";
            this.HeaderPanel.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);
            this.HeaderPanel.Size = new System.Drawing.Size(1084, 60);
            this.HeaderPanel.TabIndex = 52;
            // 
            // SubtitleLabel
            // 
            this.SubtitleLabel.AutoSize = true;
            this.SubtitleLabel.Font = new System.Drawing.Font("Segoe UI", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SubtitleLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(100)))), ((int)(((byte)(100)))));
            this.SubtitleLabel.Location = new System.Drawing.Point(182, 38);
            this.SubtitleLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.SubtitleLabel.Name = "SubtitleLabel";
            this.SubtitleLabel.Size = new System.Drawing.Size(283, 12);
            this.SubtitleLabel.TabIndex = 2;
            this.SubtitleLabel.Text = "Prevents simultaneous application launches - LRA Reflow Sorter";
            // 
            // TitleLabel
            // 
            this.TitleLabel.AutoSize = true;
            this.TitleLabel.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TitleLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(231)))), ((int)(((byte)(76)))), ((int)(((byte)(60)))));
            this.TitleLabel.Location = new System.Drawing.Point(182, 12);
            this.TitleLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.TitleLabel.Name = "TitleLabel";
            this.TitleLabel.Size = new System.Drawing.Size(326, 21);
            this.TitleLabel.TabIndex = 1;
            this.TitleLabel.Text = "Intelligent Mutex Execution Environment";
            // 
            // LogoPictureBox
            // 
            this.LogoPictureBox.Dock = System.Windows.Forms.DockStyle.Left;
            this.LogoPictureBox.Image = global::InstanceManager.Properties.Resources.Logo;
            this.LogoPictureBox.Location = new System.Drawing.Point(10, 8);
            this.LogoPictureBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.LogoPictureBox.Name = "LogoPictureBox";
            this.LogoPictureBox.Size = new System.Drawing.Size(170, 44);
            this.LogoPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.LogoPictureBox.TabIndex = 0;
            this.LogoPictureBox.TabStop = false;
            // 
            // MainSplitContainer
            // 
            this.MainSplitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.MainSplitContainer.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.MainSplitContainer.Location = new System.Drawing.Point(0, 60);
            this.MainSplitContainer.Name = "MainSplitContainer";
            // 
            // MainSplitContainer.Panel1 - Group List
            // 
            this.MainSplitContainer.Panel1.Controls.Add(this.GroupListBox);
            this.MainSplitContainer.Panel1.Controls.Add(this.GroupButtonPanel);
            this.MainSplitContainer.Panel1.Controls.Add(this.GroupLabel);
            this.MainSplitContainer.Panel1MinSize = 180;
            // 
            // MainSplitContainer.Panel2 - App List
            // 
            this.MainSplitContainer.Panel2.Controls.Add(this.AppListView);
            this.MainSplitContainer.Panel2.Controls.Add(this.ButtonPanel);
            this.MainSplitContainer.Panel2.Controls.Add(this.SelectedGroupLabel);
            this.MainSplitContainer.Size = new System.Drawing.Size(1084, 310);
            this.MainSplitContainer.SplitterDistance = 200;
            this.MainSplitContainer.TabIndex = 53;
            // 
            // GroupLabel
            // 
            this.GroupLabel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.GroupLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this.GroupLabel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.GroupLabel.ForeColor = System.Drawing.Color.White;
            this.GroupLabel.Location = new System.Drawing.Point(0, 0);
            this.GroupLabel.Name = "GroupLabel";
            this.GroupLabel.Padding = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this.GroupLabel.Size = new System.Drawing.Size(200, 28);
            this.GroupLabel.TabIndex = 0;
            this.GroupLabel.Text = "Groups";
            this.GroupLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // GroupListBox
            // 
            this.GroupListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.GroupListBox.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.GroupListBox.FormattingEnabled = true;
            this.GroupListBox.ItemHeight = 15;
            this.GroupListBox.Location = new System.Drawing.Point(0, 28);
            this.GroupListBox.Name = "GroupListBox";
            this.GroupListBox.Size = new System.Drawing.Size(200, 242);
            this.GroupListBox.TabIndex = 1;
            this.GroupListBox.SelectedIndexChanged += new System.EventHandler(this.GroupListBox_SelectedIndexChanged);
            // 
            // GroupButtonPanel
            // 
            this.GroupButtonPanel.BackColor = System.Drawing.Color.WhiteSmoke;
            this.GroupButtonPanel.Controls.Add(this.AddGroupButton);
            this.GroupButtonPanel.Controls.Add(this.EditGroupButton);
            this.GroupButtonPanel.Controls.Add(this.DeleteGroupButton);
            this.GroupButtonPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.GroupButtonPanel.Location = new System.Drawing.Point(0, 270);
            this.GroupButtonPanel.Name = "GroupButtonPanel";
            this.GroupButtonPanel.Padding = new System.Windows.Forms.Padding(4);
            this.GroupButtonPanel.Size = new System.Drawing.Size(200, 40);
            this.GroupButtonPanel.TabIndex = 2;
            // 
            // AddGroupButton
            // 
            this.AddGroupButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(41)))), ((int)(((byte)(128)))), ((int)(((byte)(185)))));
            this.AddGroupButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.AddGroupButton.Font = new System.Drawing.Font("Segoe UI", 7F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.AddGroupButton.ForeColor = System.Drawing.Color.White;
            this.AddGroupButton.Location = new System.Drawing.Point(4, 6);
            this.AddGroupButton.Name = "AddGroupButton";
            this.AddGroupButton.Size = new System.Drawing.Size(60, 28);
            this.AddGroupButton.TabIndex = 0;
            this.AddGroupButton.Text = "Add";
            this.AddGroupButton.UseVisualStyleBackColor = false;
            this.AddGroupButton.Click += new System.EventHandler(this.AddGroupButton_Click);
            // 
            // EditGroupButton
            // 
            this.EditGroupButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(243)))), ((int)(((byte)(156)))), ((int)(((byte)(18)))));
            this.EditGroupButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.EditGroupButton.Font = new System.Drawing.Font("Segoe UI", 7F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.EditGroupButton.ForeColor = System.Drawing.Color.White;
            this.EditGroupButton.Location = new System.Drawing.Point(68, 6);
            this.EditGroupButton.Name = "EditGroupButton";
            this.EditGroupButton.Size = new System.Drawing.Size(60, 28);
            this.EditGroupButton.TabIndex = 1;
            this.EditGroupButton.Text = "Edit";
            this.EditGroupButton.UseVisualStyleBackColor = false;
            this.EditGroupButton.Click += new System.EventHandler(this.EditGroupButton_Click);
            // 
            // DeleteGroupButton
            // 
            this.DeleteGroupButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(57)))), ((int)(((byte)(43)))));
            this.DeleteGroupButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.DeleteGroupButton.Font = new System.Drawing.Font("Segoe UI", 7F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.DeleteGroupButton.ForeColor = System.Drawing.Color.White;
            this.DeleteGroupButton.Location = new System.Drawing.Point(132, 6);
            this.DeleteGroupButton.Name = "DeleteGroupButton";
            this.DeleteGroupButton.Size = new System.Drawing.Size(60, 28);
            this.DeleteGroupButton.TabIndex = 2;
            this.DeleteGroupButton.Text = "Delete";
            this.DeleteGroupButton.UseVisualStyleBackColor = false;
            this.DeleteGroupButton.Click += new System.EventHandler(this.DeleteGroupButton_Click);
            // 
            // SelectedGroupLabel
            // 
            this.SelectedGroupLabel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(80)))));
            this.SelectedGroupLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this.SelectedGroupLabel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SelectedGroupLabel.ForeColor = System.Drawing.Color.White;
            this.SelectedGroupLabel.Location = new System.Drawing.Point(0, 0);
            this.SelectedGroupLabel.Name = "SelectedGroupLabel";
            this.SelectedGroupLabel.Padding = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this.SelectedGroupLabel.Size = new System.Drawing.Size(880, 28);
            this.SelectedGroupLabel.TabIndex = 0;
            this.SelectedGroupLabel.Text = "Select a group to manage applications";
            this.SelectedGroupLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // AppListView
            // 
            this.AppListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.IndexColumn,
            this.AppNameColumn,
            this.DirectoryColumn,
            this.StatusColumn,
            this.KeepOpenColumn,
            this.CrashCountColumn,
            this.RetryCountColumn,
            this.LastStartColumn,
            this.LastStopColumn});
            this.AppListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.AppListView.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.AppListView.FullRowSelect = true;
            this.AppListView.GridLines = true;
            this.AppListView.HideSelection = false;
            this.AppListView.Location = new System.Drawing.Point(0, 28);
            this.AppListView.MultiSelect = false;
            this.AppListView.Name = "AppListView";
            this.AppListView.Size = new System.Drawing.Size(880, 232);
            this.AppListView.TabIndex = 1;
            this.AppListView.UseCompatibleStateImageBehavior = false;
            this.AppListView.View = System.Windows.Forms.View.Details;
            // 
            // IndexColumn
            // 
            this.IndexColumn.Text = "#";
            this.IndexColumn.Width = 35;
            // 
            // AppNameColumn
            // 
            this.AppNameColumn.Text = "Application";
            this.AppNameColumn.Width = 120;
            // 
            // DirectoryColumn
            // 
            this.DirectoryColumn.Text = "Directory";
            this.DirectoryColumn.Width = 220;
            // 
            // StatusColumn
            // 
            this.StatusColumn.Text = "Status";
            this.StatusColumn.Width = 65;
            // 
            // KeepOpenColumn
            // 
            this.KeepOpenColumn.Text = "Keep Open";
            this.KeepOpenColumn.Width = 70;
            // 
            // CrashCountColumn
            // 
            this.CrashCountColumn.Text = "Crashes";
            this.CrashCountColumn.Width = 55;
            // 
            // RetryCountColumn
            // 
            this.RetryCountColumn.Text = "Retries";
            this.RetryCountColumn.Width = 50;
            // 
            // LastStartColumn
            // 
            this.LastStartColumn.Text = "Last Start";
            this.LastStartColumn.Width = 130;
            // 
            // LastStopColumn
            // 
            this.LastStopColumn.Text = "Last Stop";
            this.LastStopColumn.Width = 130;
            // 
            // ButtonPanel
            // 
            this.ButtonPanel.BackColor = System.Drawing.Color.WhiteSmoke;
            this.ButtonPanel.Controls.Add(this.LogsButton);
            this.ButtonPanel.Controls.Add(this.RefreshButton);
            this.ButtonPanel.Controls.Add(this.StopAllButton);
            this.ButtonPanel.Controls.Add(this.StartAllButton);
            this.ButtonPanel.Controls.Add(this.StopButton);
            this.ButtonPanel.Controls.Add(this.StartButton);
            this.ButtonPanel.Controls.Add(this.DeleteButton);
            this.ButtonPanel.Controls.Add(this.EditButton);
            this.ButtonPanel.Controls.Add(this.AddButton);
            this.ButtonPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.ButtonPanel.Location = new System.Drawing.Point(0, 260);
            this.ButtonPanel.Name = "ButtonPanel";
            this.ButtonPanel.Padding = new System.Windows.Forms.Padding(5);
            this.ButtonPanel.Size = new System.Drawing.Size(880, 50);
            this.ButtonPanel.TabIndex = 2;
            // 
            // AddButton
            // 
            this.AddButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(41)))), ((int)(((byte)(128)))), ((int)(((byte)(185)))));
            this.AddButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.AddButton.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.AddButton.ForeColor = System.Drawing.Color.White;
            this.AddButton.Location = new System.Drawing.Point(8, 8);
            this.AddButton.Name = "AddButton";
            this.AddButton.Size = new System.Drawing.Size(88, 32);
            this.AddButton.TabIndex = 0;
            this.AddButton.Text = "Add App";
            this.AddButton.UseVisualStyleBackColor = false;
            this.AddButton.Click += new System.EventHandler(this.AddButton_Click);
            // 
            // EditButton
            // 
            this.EditButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(243)))), ((int)(((byte)(156)))), ((int)(((byte)(18)))));
            this.EditButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.EditButton.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.EditButton.ForeColor = System.Drawing.Color.White;
            this.EditButton.Location = new System.Drawing.Point(100, 8);
            this.EditButton.Name = "EditButton";
            this.EditButton.Size = new System.Drawing.Size(88, 32);
            this.EditButton.TabIndex = 1;
            this.EditButton.Text = "Edit App";
            this.EditButton.UseVisualStyleBackColor = false;
            this.EditButton.Click += new System.EventHandler(this.EditButton_Click);
            // 
            // DeleteButton
            // 
            this.DeleteButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(57)))), ((int)(((byte)(43)))));
            this.DeleteButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.DeleteButton.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.DeleteButton.ForeColor = System.Drawing.Color.White;
            this.DeleteButton.Location = new System.Drawing.Point(192, 8);
            this.DeleteButton.Name = "DeleteButton";
            this.DeleteButton.Size = new System.Drawing.Size(88, 32);
            this.DeleteButton.TabIndex = 2;
            this.DeleteButton.Text = "Delete App";
            this.DeleteButton.UseVisualStyleBackColor = false;
            this.DeleteButton.Click += new System.EventHandler(this.DeleteButton_Click);
            // 
            // StartButton
            // 
            this.StartButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(204)))), ((int)(((byte)(113)))));
            this.StartButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.StartButton.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.StartButton.ForeColor = System.Drawing.Color.White;
            this.StartButton.Location = new System.Drawing.Point(296, 8);
            this.StartButton.Name = "StartButton";
            this.StartButton.Size = new System.Drawing.Size(88, 32);
            this.StartButton.TabIndex = 3;
            this.StartButton.Text = "Start";
            this.StartButton.UseVisualStyleBackColor = false;
            this.StartButton.Click += new System.EventHandler(this.StartButton_Click);
            // 
            // StopButton
            // 
            this.StopButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(231)))), ((int)(((byte)(76)))), ((int)(((byte)(60)))));
            this.StopButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.StopButton.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.StopButton.ForeColor = System.Drawing.Color.White;
            this.StopButton.Location = new System.Drawing.Point(388, 8);
            this.StopButton.Name = "StopButton";
            this.StopButton.Size = new System.Drawing.Size(88, 32);
            this.StopButton.TabIndex = 4;
            this.StopButton.Text = "Stop";
            this.StopButton.UseVisualStyleBackColor = false;
            this.StopButton.Click += new System.EventHandler(this.StopButton_Click);
            // 
            // StartAllButton
            // 
            this.StartAllButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(39)))), ((int)(((byte)(174)))), ((int)(((byte)(96)))));
            this.StartAllButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.StartAllButton.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.StartAllButton.ForeColor = System.Drawing.Color.White;
            this.StartAllButton.Location = new System.Drawing.Point(492, 8);
            this.StartAllButton.Name = "StartAllButton";
            this.StartAllButton.Size = new System.Drawing.Size(88, 32);
            this.StartAllButton.TabIndex = 5;
            this.StartAllButton.Text = "Start All";
            this.StartAllButton.UseVisualStyleBackColor = false;
            this.StartAllButton.Click += new System.EventHandler(this.StartAllButton_Click);
            // 
            // StopAllButton
            // 
            this.StopAllButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(57)))), ((int)(((byte)(43)))));
            this.StopAllButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.StopAllButton.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.StopAllButton.ForeColor = System.Drawing.Color.White;
            this.StopAllButton.Location = new System.Drawing.Point(584, 8);
            this.StopAllButton.Name = "StopAllButton";
            this.StopAllButton.Size = new System.Drawing.Size(88, 32);
            this.StopAllButton.TabIndex = 6;
            this.StopAllButton.Text = "Stop All";
            this.StopAllButton.UseVisualStyleBackColor = false;
            this.StopAllButton.Click += new System.EventHandler(this.StopAllButton_Click);
            // 
            // LogsButton
            // 
            this.LogsButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(155)))), ((int)(((byte)(89)))), ((int)(((byte)(182)))));
            this.LogsButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.LogsButton.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LogsButton.ForeColor = System.Drawing.Color.White;
            this.LogsButton.Location = new System.Drawing.Point(676, 8);
            this.LogsButton.Name = "LogsButton";
            this.LogsButton.Size = new System.Drawing.Size(88, 32);
            this.LogsButton.TabIndex = 7;
            this.LogsButton.Text = "Logs";
            this.LogsButton.UseVisualStyleBackColor = false;
            this.LogsButton.Click += new System.EventHandler(this.LogsButton_Click);
            // 
            // RefreshButton
            // 
            this.RefreshButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.RefreshButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.RefreshButton.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.RefreshButton.ForeColor = System.Drawing.Color.White;
            this.RefreshButton.Location = new System.Drawing.Point(768, 8);
            this.RefreshButton.Name = "RefreshButton";
            this.RefreshButton.Size = new System.Drawing.Size(88, 32);
            this.RefreshButton.TabIndex = 8;
            this.RefreshButton.Text = "Refresh";
            this.RefreshButton.UseVisualStyleBackColor = false;
            this.RefreshButton.Click += new System.EventHandler(this.RefreshButton_Click);
            // 
            // Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1084, 370);
            this.Controls.Add(this.MainSplitContainer);
            this.Controls.Add(this.HeaderPanel);
            this.Name = "Main";
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "Local MA";
            this.Load += new System.EventHandler(this.Main_Load);
            this.HeaderPanel.ResumeLayout(false);
            this.HeaderPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.LogoPictureBox)).EndInit();
            this.MainSplitContainer.Panel1.ResumeLayout(false);
            this.MainSplitContainer.Panel2.ResumeLayout(false);
            this.MainSplitContainer.ResumeLayout(false);
            this.GroupButtonPanel.ResumeLayout(false);
            this.ButtonPanel.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel HeaderPanel;
        private System.Windows.Forms.Label SubtitleLabel;
        private System.Windows.Forms.Label TitleLabel;
        private System.Windows.Forms.PictureBox LogoPictureBox;
        private System.Windows.Forms.SplitContainer MainSplitContainer;
        private System.Windows.Forms.Label GroupLabel;
        private System.Windows.Forms.ListBox GroupListBox;
        private System.Windows.Forms.Panel GroupButtonPanel;
        private System.Windows.Forms.Button AddGroupButton;
        private System.Windows.Forms.Button EditGroupButton;
        private System.Windows.Forms.Button DeleteGroupButton;
        private System.Windows.Forms.Label SelectedGroupLabel;
        private System.Windows.Forms.ListView AppListView;
        private System.Windows.Forms.ColumnHeader IndexColumn;
        private System.Windows.Forms.ColumnHeader AppNameColumn;
        private System.Windows.Forms.ColumnHeader DirectoryColumn;
        private System.Windows.Forms.ColumnHeader StatusColumn;
        private System.Windows.Forms.ColumnHeader KeepOpenColumn;
        private System.Windows.Forms.ColumnHeader CrashCountColumn;
        private System.Windows.Forms.ColumnHeader RetryCountColumn;
        private System.Windows.Forms.ColumnHeader LastStartColumn;
        private System.Windows.Forms.ColumnHeader LastStopColumn;
        private System.Windows.Forms.Panel ButtonPanel;
        private System.Windows.Forms.Button AddButton;
        private System.Windows.Forms.Button EditButton;
        private System.Windows.Forms.Button DeleteButton;
        private System.Windows.Forms.Button StartButton;
        private System.Windows.Forms.Button StopButton;
        private System.Windows.Forms.Button StartAllButton;
        private System.Windows.Forms.Button StopAllButton;
        private System.Windows.Forms.Button LogsButton;
        private System.Windows.Forms.Button RefreshButton;
    }
}

