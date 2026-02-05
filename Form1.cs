using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Utilities;

namespace InstanceManager
{
    public partial class Main : Form
    {
        private StorageService _storageService;
        private ProcessManager _processManager;
        private Timer _statusUpdateTimer;

        public Main()
        {
            InitializeComponent();
            InitializeServices();
            SetupTimer();
            LoadApplications();
            SimpleLogger.Info("Main @ Form1.cs", "Application started");
        }

        private void Main_Load(object sender, EventArgs e)
        {
            int screenWidth = Screen.PrimaryScreen.WorkingArea.Width;
            int screenHeight = Screen.PrimaryScreen.WorkingArea.Height;
            this.Left = screenWidth - this.Width - 10;
            this.Top = screenHeight - this.Height - 10;
        }

        private void InitializeServices()
        {
            _storageService = new StorageService();
            _processManager = new ProcessManager();
        }

        private void SetupTimer()
        {
            _statusUpdateTimer = new Timer();
            _statusUpdateTimer.Interval = 2000;
            _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
            _statusUpdateTimer.Start();
        }

        private void StatusUpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateApplicationStatuses();
        }

        private void LoadApplications()
        {
            try
            {
                var apps = _storageService.GetAllApplications();
                AppListView.Items.Clear();

                foreach (var app in apps)
                {
                    AddApplicationToListView(app);
                }

                SimpleLogger.Info("LoadApplications @ Form1.cs", $"Loaded {apps.Count} applications");
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("LoadApplications @ Form1.cs", $"Error loading applications: {ex.Message}");
                MessageBoxHelper.ShowError(this, $"Error loading applications: {ex.Message}");
            }
        }

        private void AddApplicationToListView(ManagedApplication app)
        {
            bool isRunning = _processManager.IsApplicationRunning(app);
            app.IsRunning = isRunning;

            ListViewItem item = new ListViewItem(app.Index.ToString());
            item.SubItems.Add(app.AppName);
            item.SubItems.Add(app.Directory);
            item.SubItems.Add(isRunning ? "Running" : "Stopped");
            item.SubItems.Add(app.GetLastStartDisplay());
            item.SubItems.Add(app.GetLastStopDisplay());
            item.Tag = app;
            item.ForeColor = isRunning ? Color.Green : Color.Black;

            AppListView.Items.Add(item);
        }

        private void UpdateApplicationStatuses()
        {
            try
            {
                foreach (ListViewItem item in AppListView.Items)
                {
                    if (item.Tag is ManagedApplication app)
                    {
                        bool isRunning = _processManager.IsApplicationRunning(app);
                        app.IsRunning = isRunning;

                        if (item.SubItems[3].Text != (isRunning ? "Running" : "Stopped"))
                        {
                            item.SubItems[3].Text = isRunning ? "Running" : "Stopped";
                            item.ForeColor = isRunning ? Color.Green : Color.Black;
                            _storageService.UpdateApplication(app);
                        }
                        
                        // Update Last Start and Last Stop displays
                        item.SubItems[4].Text = app.GetLastStartDisplay();
                        item.SubItems[5].Text = app.GetLastStopDisplay();
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("UpdateApplicationStatuses @ Form1.cs", $"Error updating statuses: {ex.Message}");
            }
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            try
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*";
                    dialog.Title = "Select Application to Manage";

                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        string appPath = dialog.FileName;
                        string appName = Path.GetFileNameWithoutExtension(appPath);

                        // Check for duplicates
                        if (_storageService.ApplicationExists(appPath))
                        {
                            SimpleLogger.Warn("AddButton_Click @ Form1.cs", $"Duplicate application detected: {appName} at {appPath}");
                            MessageBoxHelper.ShowWarning(this, 
                                $"Application '{appName}' already exists in the management list.\n\nPath: {appPath}");
                            return;
                        }

                        ManagedApplication app = new ManagedApplication
                        {
                            AppName = appName,
                            Directory = appPath
                        };

                        _storageService.AddApplication(app);
                        AddApplicationToListView(app);

                        SimpleLogger.Info("AddButton_Click @ Form1.cs", $"Added new application: {appName}");
                        MessageBoxHelper.ShowSuccess(this, $"Application '{appName}' added successfully!");
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("AddButton_Click @ Form1.cs", $"Error adding application: {ex.Message}");
                MessageBoxHelper.ShowError(this, $"Error adding application: {ex.Message}");
            }
        }

        private void EditButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (AppListView.SelectedItems.Count == 0)
                {
                    MessageBoxHelper.ShowInfo(this, "Please select an application to edit.");
                    return;
                }

                var selectedItem = AppListView.SelectedItems[0];
                var app = selectedItem.Tag as ManagedApplication;

                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*";
                    dialog.Title = "Select New Application Path";
                    dialog.FileName = app.Directory;

                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        string newPath = dialog.FileName;
                        
                        // Check if the new path is different and if it already exists
                        if (newPath != app.Directory && _storageService.ApplicationExists(newPath))
                        {
                            string newAppName = Path.GetFileNameWithoutExtension(newPath);
                            SimpleLogger.Warn("EditButton_Click @ Form1.cs", $"Duplicate application detected: {newAppName} at {newPath}");
                            MessageBoxHelper.ShowWarning(this,
                                $"Application '{newAppName}' already exists in the management list.\n\nPath: {newPath}");
                            return;
                        }

                        string oldName = app.AppName;
                        app.AppName = Path.GetFileNameWithoutExtension(newPath);
                        app.Directory = newPath;

                        _storageService.UpdateApplication(app);

                        selectedItem.SubItems[1].Text = app.AppName;
                        selectedItem.SubItems[2].Text = app.Directory;

                        SimpleLogger.Info("EditButton_Click @ Form1.cs", $"Edited application: {oldName} -> {app.AppName}");
                        MessageBoxHelper.ShowSuccess(this, "Application updated successfully!");
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("EditButton_Click @ Form1.cs", $"Error editing application: {ex.Message}");
                MessageBoxHelper.ShowError(this, $"Error editing application: {ex.Message}");
            }
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (AppListView.SelectedItems.Count == 0)
                {
                    MessageBoxHelper.ShowInfo(this, "Please select an application to delete.");
                    return;
                }

                var selectedItem = AppListView.SelectedItems[0];
                var app = selectedItem.Tag as ManagedApplication;

                var result = MessageBoxHelper.ShowQuestion(this,
                    $"Are you sure you want to remove '{app.AppName}' from management?\n\nNote: This will not delete the actual application file.",
                    "Confirm Delete");

                if (result == DialogResult.Yes)
                {
                    _storageService.RemoveApplication(app.Index);
                    AppListView.Items.Remove(selectedItem);

                    SimpleLogger.Info("DeleteButton_Click @ Form1.cs", $"Deleted application: {app.AppName}");
                    MessageBoxHelper.ShowSuccess(this, $"Application '{app.AppName}' removed from management.");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("DeleteButton_Click @ Form1.cs", $"Error deleting application: {ex.Message}");
                MessageBoxHelper.ShowError(this, $"Error deleting application: {ex.Message}");
            }
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (AppListView.SelectedItems.Count == 0)
                {
                    MessageBoxHelper.ShowInfo(this, "Please select an application to start.");
                    return;
                }

                var selectedItem = AppListView.SelectedItems[0];
                var app = selectedItem.Tag as ManagedApplication;

                if (_processManager.IsApplicationRunning(app))
                {
                    MessageBoxHelper.ShowInfo(this, $"'{app.AppName}' is already running!");
                    return;
                }

                if (_processManager.StartApplication(app))
                {
                    app.LastStart = DateTime.Now;
                    selectedItem.SubItems[3].Text = "Running";
                    selectedItem.SubItems[4].Text = app.GetLastStartDisplay();
                    selectedItem.ForeColor = Color.Green;
                    app.IsRunning = true;
                    _storageService.UpdateApplication(app);

                    MessageBoxHelper.ShowSuccess(this, $"'{app.AppName}' started successfully!");
                }
                else
                {
                    MessageBoxHelper.ShowError(this, $"Failed to start '{app.AppName}'. Check logs for details.");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("StartButton_Click @ Form1.cs", $"Error starting application: {ex.Message}");
                MessageBoxHelper.ShowError(this, $"Error starting application: {ex.Message}");
            }
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (AppListView.SelectedItems.Count == 0)
                {
                    MessageBoxHelper.ShowInfo(this, "Please select an application to stop.");
                    return;
                }

                var selectedItem = AppListView.SelectedItems[0];
                var app = selectedItem.Tag as ManagedApplication;

                if (!_processManager.IsApplicationRunning(app))
                {
                    MessageBoxHelper.ShowInfo(this, $"'{app.AppName}' is not running!");
                    return;
                }

                var result = MessageBoxHelper.ShowQuestion(this,
                    $"Are you sure you want to stop '{app.AppName}'?",
                    "Confirm Stop");

                if (result == DialogResult.Yes)
                {
                    if (_processManager.StopApplication(app))
                    {
                        app.LastStop = DateTime.Now;
                        selectedItem.SubItems[3].Text = "Stopped";
                        selectedItem.SubItems[5].Text = app.GetLastStopDisplay();
                        selectedItem.ForeColor = Color.Black;
                        app.IsRunning = false;
                        _storageService.UpdateApplication(app);

                        MessageBoxHelper.ShowSuccess(this, $"'{app.AppName}' stopped successfully!");
                    }
                    else
                    {
                        MessageBoxHelper.ShowError(this, $"Failed to stop '{app.AppName}'. Check logs for details.");
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("StopButton_Click @ Form1.cs", $"Error stopping application: {ex.Message}");
                MessageBoxHelper.ShowError(this, $"Error stopping application: {ex.Message}");
            }
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            LoadApplications();
            MessageBoxHelper.ShowSuccess(this, "Application list refreshed!");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            
            if (_statusUpdateTimer != null)
            {
                _statusUpdateTimer.Stop();
                _statusUpdateTimer.Dispose();
            }

            SimpleLogger.Info("OnFormClosing @ Form1.cs", "Application closing");
        }
    }
}
