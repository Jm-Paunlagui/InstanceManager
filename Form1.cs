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

        // Tracks which apps were started/authorized through Instance Manager (or were running at startup)
        private HashSet<int> _authorizedApps = new HashSet<int>();
        // Prevents duplicate notifications for the same unauthorized launch
        private HashSet<int> _notifiedUnauthorized = new HashSet<int>();
        // Tracks apps that the user intentionally stopped via the Stop button
        // so the watchdog doesn't interfere during process shutdown
        private HashSet<int> _pendingStop = new HashSet<int>();

        public Main()
        {
            InitializeComponent();
            InitializeServices();
            TerminateAlreadyRunningApps();
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

        /// <summary>
        /// On startup, terminate any managed apps that were already running
        /// since they were not launched through Instance Manager.
        /// </summary>
        private void TerminateAlreadyRunningApps()
        {
            var apps = _storageService.GetAllApplications();
            List<string> terminatedApps = new List<string>();

            foreach (var app in apps)
            {
                if (_processManager.IsApplicationRunning(app))
                {
                    SimpleLogger.Warn("TerminateAlreadyRunningApps @ Form1.cs",
                        $"'{app.AppName}' was running before Instance Manager started - terminating");

                    _processManager.StopApplication(app);

                    app.LastStop = DateTime.Now;
                    app.IsRunning = false;
                    _storageService.UpdateApplication(app);

                    terminatedApps.Add(app.AppName);
                }
            }

            if (terminatedApps.Count > 0)
            {
                string appList = string.Join("\n- ", terminatedApps.ToArray());
                SimpleLogger.Warn("TerminateAlreadyRunningApps @ Form1.cs",
                    $"Terminated {terminatedApps.Count} application(s) at startup: {appList}");

                MessageBoxHelper.ShowWarning(null,
                    $"The following application(s) were running before Instance Manager started and have been terminated:\n\n" +
                    $"- {appList}\n\n" +
                    "All managed applications must be started through Instance Manager.");
            }
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
                        // Skip watchdog checks for apps the user intentionally stopped
                        if (_pendingStop.Contains(app.Index))
                        {
                            bool stillRunning = _processManager.IsApplicationRunning(app);
                            if (!stillRunning)
                            {
                                _pendingStop.Remove(app.Index);
                                // Update UI if not already updated by StopButton_Click
                                if (item.SubItems[3].Text == "Running")
                                {
                                    item.SubItems[3].Text = "Stopped";
                                    item.ForeColor = Color.Black;
                                }
                            }
                            continue;
                        }

                        bool wasRunning = item.SubItems[3].Text == "Running";
                        bool isRunning = _processManager.IsApplicationRunning(app);

                        // Detect unauthorized external launch
                        if (isRunning && !wasRunning && !_authorizedApps.Contains(app.Index))
                        {
                            HandleUnauthorizedLaunch(app, item);
                            continue;
                        }

                        // App was stopped externally (outside Instance Manager)
                        if (!isRunning && wasRunning)
                        {
                            _authorizedApps.Remove(app.Index);
                            _notifiedUnauthorized.Remove(app.Index);
                            app.IsRunning = false;
                            app.LastStop = DateTime.Now;
                            item.SubItems[3].Text = "Stopped";
                            item.SubItems[5].Text = app.GetLastStopDisplay();
                            item.ForeColor = Color.Black;
                            _storageService.UpdateApplication(app);

                            SimpleLogger.Info("UpdateApplicationStatuses @ Form1.cs",
                                $"'{app.AppName}' was stopped externally");
                        }

                        app.IsRunning = isRunning;

                        if (item.SubItems[3].Text != (isRunning ? "Running" : "Stopped"))
                        {
                            item.SubItems[3].Text = isRunning ? "Running" : "Stopped";
                            item.ForeColor = isRunning ? Color.Green : Color.Black;
                            _storageService.UpdateApplication(app);
                        }

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

        /// <summary>
        /// Handles when a managed application is launched outside of Instance Manager.
        /// Kills the unauthorized process and notifies the user.
        /// </summary>
        private void HandleUnauthorizedLaunch(ManagedApplication app, ListViewItem item)
        {
            SimpleLogger.Warn("HandleUnauthorizedLaunch @ Form1.cs",
                $"Unauthorized launch detected for '{app.AppName}' - killing process");

            // Kill the unauthorized process
            _processManager.StopApplication(app);

            // Update UI to reflect stopped state
            app.IsRunning = false;
            item.SubItems[3].Text = "Stopped";
            item.ForeColor = Color.Black;

            // Notify user only once per unauthorized attempt
            if (!_notifiedUnauthorized.Contains(app.Index))
            {
                _notifiedUnauthorized.Add(app.Index);

                int appIndex = app.Index;
                string appName = app.AppName;

                // Use BeginInvoke so the timer isn't blocked while showing the dialog
                this.BeginInvoke(new Action(() =>
                {
                    // Safety check: if user clicked Stop while this was queued, don't show warning
                    if (_pendingStop.Contains(appIndex))
                    {
                        SimpleLogger.Debug("HandleUnauthorizedLaunch @ Form1.cs",
                            $"Suppressed unauthorized warning for '{appName}' - user initiated stop");
                        return;
                    }

                    MessageBoxHelper.ShowWarning(this,
                        $"'{appName}' was launched outside of Instance Manager and has been terminated.\n\n" +
                        "Please use Instance Manager to start managed applications.");

                    SimpleLogger.Warn("HandleUnauthorizedLaunch @ Form1.cs",
                        $"User notified about unauthorized launch of '{appName}'");
                }));
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
                    _authorizedApps.Remove(app.Index);
                    _notifiedUnauthorized.Remove(app.Index);
                    _pendingStop.Remove(app.Index);
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
                    // Mark as authorized so the timer doesn't kill it
                    _authorizedApps.Add(app.Index);
                    _notifiedUnauthorized.Remove(app.Index);
                    _pendingStop.Remove(app.Index);

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

                // Mark as pending stop BEFORE showing confirmation dialog
                // This prevents the watchdog from interfering while the dialog is open
                _pendingStop.Add(app.Index);

                var result = MessageBoxHelper.ShowQuestion(this,
                    $"Are you sure you want to stop '{app.AppName}'?",
                    "Confirm Stop");

                if (result == DialogResult.Yes)
                {
                    _authorizedApps.Remove(app.Index);
                    _notifiedUnauthorized.Remove(app.Index);

                    if (_processManager.StopApplication(app))
                    {
                        app.LastStop = DateTime.Now;
                        selectedItem.SubItems[3].Text = "Stopped";
                        selectedItem.SubItems[5].Text = app.GetLastStopDisplay();
                        selectedItem.ForeColor = Color.Black;
                        app.IsRunning = false;
                        _storageService.UpdateApplication(app);

                        _pendingStop.Remove(app.Index);

                        MessageBoxHelper.ShowSuccess(this, $"'{app.AppName}' stopped successfully!");
                    }
                    else
                    {
                        _pendingStop.Remove(app.Index);
                        MessageBoxHelper.ShowError(this, $"Failed to stop '{app.AppName}'. Check logs for details.");
                    }
                }
                else
                {
                    // User cancelled — remove from pending stop
                    _pendingStop.Remove(app.Index);
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
