using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using IntelligentMutexExecutionEnvironment.Models;
using IntelligentMutexExecutionEnvironment.Services;
using IntelligentMutexExecutionEnvironment.Utilities;

namespace IntelligentMutexExecutionEnvironment
{
    public partial class Main : Form
    {
        private StorageService _storageService;
        private ProcessManager _processManager;
        private SettingsService _settingsService;
        private Timer _statusUpdateTimer;

        // Tracks which apps were started/authorized through Instance Manager (or were running at startup)
        private HashSet<int> _authorizedApps = new HashSet<int>();
        // Prevents duplicate notifications for the same unauthorized launch
        private HashSet<int> _notifiedUnauthorized = new HashSet<int>();
        // Tracks apps that the user intentionally stopped via the Stop button
        // so the watchdog doesn't interfere during process shutdown
        private HashSet<int> _pendingStop = new HashSet<int>();
        // Prevents re-entrant timer ticks while a tick is still executing
        private bool _isUpdatingStatuses;
        // Currently selected group ID (-1 means no group selected)
        private int _selectedGroupId = -1;
        // Prevents timer ticks from running after form begins closing
        private bool _isClosing;
        // Tracks when a KeepOpen app crashed and is pending restart (app Index -> restart-eligible time)
        private Dictionary<int, DateTime> _pendingRestart = new Dictionary<int, DateTime>();
        // Tracks apps that have exhausted max retries and should remain in "Failed" state
        private HashSet<int> _failedApps = new HashSet<int>();
        // Tracks apps that are pending sequential startup launch (Start All with delays)
        // so the status update timer doesn't overwrite their countdown text
        private HashSet<int> _pendingSequentialStart = new HashSet<int>();
        // Tracks apps that were recently started and are in a grace period
        // to allow the process window to appear before watchdog monitoring begins
        private Dictionary<int, DateTime> _startGracePeriod = new Dictionary<int, DateTime>();
        // Grace period in seconds after starting an app before watchdog monitoring begins
        private const int StartGracePeriodSeconds = 10;
        // Dedicated 1-second timer for smooth restart countdown display
        private Timer _countdownTimer;

        public Main()
        {
            InitializeComponent();
            try
            {
                InitializeServices();
                UpdateStationNameDisplay();
                TerminateAlreadyRunningApps();
                SetupTimer();
                LoadGroups();
                UpdateAppButtonsEnabled();
                SimpleLogger.Info("Main @ Form1.cs", "Application started");
            }
            catch (Exception ex)
            {
                SimpleLogger.Fatal("Main @ Form1.cs", $"Critical error during startup: {ex.Message}");
                MessageBoxHelper.ShowError(this, $"Failed to initialize application:\n\n{ex.Message}");
            }
        }

        private void Main_Load(object sender, EventArgs e)
        {
            try
            {
                int screenWidth = Screen.PrimaryScreen.WorkingArea.Width;
                int screenHeight = Screen.PrimaryScreen.WorkingArea.Height;
                this.Left = screenWidth - this.Width - 10;
                this.Top = screenHeight - this.Height - 10;
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("Main_Load @ Form1.cs", $"Error positioning form: {ex.Message}");
            }
        }

        private void InitializeServices()
        {
            _storageService = new StorageService();
            _processManager = new ProcessManager();
            _settingsService = new SettingsService();
        }

        /// <summary>
        /// On startup, terminate any managed apps that were already running
        /// since they were not launched through Instance Manager.
        /// Also resets stale runtime state (IsRunning, RetryCount) for all apps
        /// to prevent false crash detections and accumulated retry counts from previous sessions.
        /// </summary>
        private void TerminateAlreadyRunningApps()
        {
            try
            {
                var apps = _storageService.GetAllApplicationsReadOnly();
                List<string> terminatedApps = new List<string>();

                // Single process enumeration for all apps instead of one per app
                var snapshots = _processManager.GetBatchProcessSnapshot(apps);

                foreach (var app in apps)
                {
                    if (string.IsNullOrEmpty(app.Directory))
                        continue;

                    ProcessManager.ProcessSnapshot snapshot;
                    bool isRunning = snapshots.TryGetValue(app.Index, out snapshot) && snapshot.HasWindowedProcess;

                    if (isRunning)
                    {
                        SimpleLogger.Warn("TerminateAlreadyRunningApps @ Form1.cs",
                            $"'{app.AppName}' was running before Instance Manager started - terminating");

                        _processManager.StopApplication(app);

                        _storageService.UpdateApplicationFields(app.Index, isRunning: false, lastStop: DateTime.Now, retryCount: 0);

                        terminatedApps.Add(app.AppName ?? "(unknown)");
                    }
                    else
                    {
                        // Reset stale runtime state from previous session
                        _storageService.UpdateApplicationFields(app.Index, isRunning: false, retryCount: 0);
                    }
                }

                // Flush the changes immediately since this is a one-time startup operation
                _storageService.FlushPendingChanges();

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
            catch (Exception ex)
            {
                SimpleLogger.Error("TerminateAlreadyRunningApps @ Form1.cs", $"Error during startup termination: {ex.Message}");
            }
        }

        private void SetupTimer()
        {
            _statusUpdateTimer = new Timer();
            _statusUpdateTimer.Interval = 5000;
            _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
            _statusUpdateTimer.Start();

            _countdownTimer = new Timer();
            _countdownTimer.Interval = 1000;
            _countdownTimer.Tick += CountdownTimer_Tick;
        }

        private void StatusUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (_isClosing || _isUpdatingStatuses) return;
            _isUpdatingStatuses = true;
            try
            {
                UpdateApplicationStatuses();
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("StatusUpdateTimer_Tick @ Form1.cs", $"Unhandled error in timer tick: {ex.Message}");
            }
            finally
            {
                _isUpdatingStatuses = false;
            }
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            if (_isClosing) return;

            try
            {
                if (_pendingRestart.Count == 0)
                {
                    _countdownTimer.Stop();
                    return;
                }

                // Build ListView index only if the selected group is visible
                Dictionary<int, ListViewItem> listViewIndex = null;
                if (_selectedGroupId > 0 && AppListView.Items.Count > 0)
                {
                    listViewIndex = new Dictionary<int, ListViewItem>(AppListView.Items.Count);
                    foreach (ListViewItem lvi in AppListView.Items)
                    {
                        var tagApp = lvi.Tag as ManagedApplication;
                        if (tagApp != null)
                        {
                            listViewIndex[tagApp.Index] = lvi;
                        }
                    }
                }

                foreach (var kvp in _pendingRestart)
                {
                    ListViewItem item = null;
                    if (listViewIndex != null)
                    {
                        listViewIndex.TryGetValue(kvp.Key, out item);
                    }

                    if (item != null)
                    {
                        int secondsLeft = (int)Math.Ceiling((kvp.Value - DateTime.Now).TotalSeconds);
                        if (secondsLeft <= 0)
                        {
                            item.SubItems[3].Text = "Starting...";
                        }
                        else
                        {
                            item.SubItems[3].Text = $"Restarting ({secondsLeft}s)";
                        }
                        item.ForeColor = Color.DarkOrange;
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("CountdownTimer_Tick @ Form1.cs", $"Error in countdown tick: {ex.Message}");
            }
        }

        // ===== Group Management =====

        private void LoadGroups()
        {
            try
            {
                var groups = _storageService.GetAllGroups();
                GroupListBox.Items.Clear();

                foreach (var group in groups)
                {
                    GroupListBox.Items.Add(group);
                }

                SimpleLogger.Info("LoadGroups @ Form1.cs", $"Loaded {groups.Count} groups");
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("LoadGroups @ Form1.cs", $"Error loading groups: {ex.Message}");
                MessageBoxHelper.ShowError(this, $"Error loading groups: {ex.Message}");
            }
        }

        private void GroupListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                var selectedGroup = GroupListBox.SelectedItem as ApplicationGroup;
                if (selectedGroup != null)
                {
                    _selectedGroupId = selectedGroup.GroupId;
                    SelectedGroupLabel.Text = $"Group: {selectedGroup.GroupName}";
                    LoadApplicationsForGroup(selectedGroup.GroupId);
                }
                else
                {
                    _selectedGroupId = -1;
                    SelectedGroupLabel.Text = "Select a group to manage applications";
                    AppListView.Items.Clear();
                }
                UpdateAppButtonsEnabled();
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("GroupListBox_SelectedIndexChanged @ Form1.cs", $"Error selecting group: {ex.Message}");
            }
        }

        private void UpdateAppButtonsEnabled()
        {
            bool groupSelected = _selectedGroupId > 0;
            AddButton.Enabled = groupSelected;
            EditButton.Enabled = groupSelected;
            DeleteButton.Enabled = groupSelected;
            StartButton.Enabled = groupSelected;
            StopButton.Enabled = groupSelected;
            StartAllButton.Enabled = groupSelected;
            StopAllButton.Enabled = groupSelected;
            LogsButton.Enabled = true;
            RefreshButton.Enabled = groupSelected;
        }

        private void AddGroupButton_Click(object sender, EventArgs e)
        {
            try
            {
                string groupName;
                var result = InputDialog.Show(this, "Add Group", "Enter group name:", "", out groupName);

                if (result == DialogResult.OK)
                {
                    if (string.IsNullOrEmpty(groupName))
                    {
                        MessageBoxHelper.ShowWarning(this, "Group name cannot be empty.");
                        return;
                    }

                    if (_storageService.GroupNameExists(groupName))
                    {
                        MessageBoxHelper.ShowWarning(this, $"A group named '{groupName}' already exists.");
                        return;
                    }

                    var group = new ApplicationGroup { GroupName = groupName };
                    _storageService.AddGroup(group);
                    GroupListBox.Items.Add(group);

                    // Auto-select the new group
                    GroupListBox.SelectedItem = group;

                    SimpleLogger.Info("AddGroupButton_Click @ Form1.cs", $"Added group: {groupName}");
                    MessageBoxHelper.ShowSuccess(this, $"Group '{groupName}' created successfully!");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("AddGroupButton_Click @ Form1.cs", $"Error adding group: {ex.Message}");
                MessageBoxHelper.ShowError(this, $"Error adding group: {ex.Message}");
            }
        }

        private void EditGroupButton_Click(object sender, EventArgs e)
        {
            try
            {
                var selectedGroup = GroupListBox.SelectedItem as ApplicationGroup;
                if (selectedGroup == null)
                {
                    MessageBoxHelper.ShowInfo(this, "Please select a group to edit.");
                    return;
                }

                string newName;
                var result = InputDialog.Show(this, "Edit Group", "Enter new group name:", selectedGroup.GroupName, out newName);

                if (result == DialogResult.OK)
                {
                    if (string.IsNullOrEmpty(newName))
                    {
                        MessageBoxHelper.ShowWarning(this, "Group name cannot be empty.");
                        return;
                    }

                    if (_storageService.GroupNameExists(newName, selectedGroup.GroupId))
                    {
                        MessageBoxHelper.ShowWarning(this, $"A group named '{newName}' already exists.");
                        return;
                    }

                    string oldName = selectedGroup.GroupName;
                    selectedGroup.GroupName = newName;
                    _storageService.UpdateGroup(selectedGroup);

                    // Refresh the listbox display
                    int idx = GroupListBox.SelectedIndex;
                    GroupListBox.Items[idx] = selectedGroup;
                    SelectedGroupLabel.Text = $"Group: {selectedGroup.GroupName}";

                    SimpleLogger.Info("EditGroupButton_Click @ Form1.cs", $"Renamed group: {oldName} -> {newName}");
                    MessageBoxHelper.ShowSuccess(this, $"Group renamed to '{newName}' successfully!");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("EditGroupButton_Click @ Form1.cs", $"Error editing group: {ex.Message}");
                MessageBoxHelper.ShowError(this, $"Error editing group: {ex.Message}");
            }
        }

        private void DeleteGroupButton_Click(object sender, EventArgs e)
        {
            try
            {
                var selectedGroup = GroupListBox.SelectedItem as ApplicationGroup;
                if (selectedGroup == null)
                {
                    MessageBoxHelper.ShowInfo(this, "Please select a group to delete.");
                    return;
                }

                var appsInGroup = _storageService.GetApplicationsByGroup(selectedGroup.GroupId);
                string warning = $"Are you sure you want to delete group '{selectedGroup.GroupName}'?";
                if (appsInGroup.Count > 0)
                {
                    warning += $"\n\nThis will also remove {appsInGroup.Count} application(s) from management.";
                }

                var result = MessageBoxHelper.ShowQuestion(this, warning, "Confirm Delete Group");

                if (result == DialogResult.Yes)
                {
                    // Stop any running apps in this group first
                    foreach (var app in appsInGroup)
                    {
                        if (!string.IsNullOrEmpty(app.Directory) && _processManager.IsApplicationRunning(app))
                        {
                            _processManager.StopApplication(app);
                        }
                        _authorizedApps.Remove(app.Index);
                        _notifiedUnauthorized.Remove(app.Index);
                        _pendingStop.Remove(app.Index);
                        _pendingRestart.Remove(app.Index);
                        _failedApps.Remove(app.Index);
                        _startGracePeriod.Remove(app.Index);
                    }

                    string groupName = selectedGroup.GroupName;
                    _storageService.RemoveGroup(selectedGroup.GroupId);
                    GroupListBox.Items.Remove(selectedGroup);

                    _selectedGroupId = -1;
                    SelectedGroupLabel.Text = "Select a group to manage applications";
                    AppListView.Items.Clear();
                    UpdateAppButtonsEnabled();

                    SimpleLogger.Info("DeleteGroupButton_Click @ Form1.cs", $"Deleted group: {groupName}");
                    MessageBoxHelper.ShowSuccess(this, $"Group '{groupName}' and its applications removed.");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("DeleteGroupButton_Click @ Form1.cs", $"Error deleting group: {ex.Message}");
                MessageBoxHelper.ShowError(this, $"Error deleting group: {ex.Message}");
            }
        }

        // ===== Application Management =====

        private void LoadApplicationsForGroup(int groupId)
        {
            try
            {
                var apps = _storageService.GetApplicationsByGroup(groupId);
                AppListView.Items.Clear();

                foreach (var app in apps)
                {
                    AddApplicationToListView(app);
                }

                SimpleLogger.Info("LoadApplicationsForGroup @ Form1.cs", $"Loaded {apps.Count} applications for group {groupId}");
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("LoadApplicationsForGroup @ Form1.cs", $"Error loading applications: {ex.Message}");
                MessageBoxHelper.ShowError(this, $"Error loading applications: {ex.Message}");
            }
        }

        private void AddApplicationToListView(ManagedApplication app)
        {
            bool isRunning = false;
            if (!string.IsNullOrEmpty(app.Directory))
            {
                isRunning = _processManager.IsApplicationRunning(app);
            }
            app.IsRunning = isRunning;

            ListViewItem item = new ListViewItem(app.Index.ToString());
            item.SubItems.Add(app.AppName ?? "");                    // [1] Application
            item.SubItems.Add(app.Directory ?? "");                  // [2] Directory
            item.SubItems.Add(isRunning ? "Running" : "Stopped");    // [3] Status
            item.SubItems.Add(app.GetKeepOpenDisplay());             // [4] Keep Open
            item.SubItems.Add(app.CrashCount.ToString());            // [5] Crashes
            item.SubItems.Add(app.RetryCount.ToString());            // [6] Retries
            item.SubItems.Add(app.GetLastStartDisplay());            // [7] Last Start
            item.SubItems.Add(app.GetLastStopDisplay());             // [8] Last Stop
            item.Tag = app;
            item.ForeColor = isRunning ? Color.Green : Color.Black;

            AppListView.Items.Add(item);
        }

        private void UpdateApplicationStatuses()
        {
            try
            {
                // Update statuses for ALL managed apps (not just the visible group)
                // to enforce watchdog across all groups
                var allApps = _storageService.GetAllApplicationsReadOnly();

                // Single process enumeration for ALL apps — replaces N individual
                // GetProcessesByName calls with one Process.GetProcesses() call
                var snapshots = _processManager.GetBatchProcessSnapshot(allApps);

                // Build an index of ListView items by app Index for O(1) lookup
                // instead of O(n) linear scan per app
                Dictionary<int, ListViewItem> listViewIndex = null;
                if (_selectedGroupId > 0 && AppListView.Items.Count > 0)
                {
                    listViewIndex = new Dictionary<int, ListViewItem>(AppListView.Items.Count);
                    foreach (ListViewItem lvi in AppListView.Items)
                    {
                        var tagApp = lvi.Tag as ManagedApplication;
                        if (tagApp != null)
                        {
                            listViewIndex[tagApp.Index] = lvi;
                        }
                    }
                }

                foreach (var app in allApps)
                {
                    if (string.IsNullOrEmpty(app.Directory))
                        continue;

                    // Find the corresponding ListView item using O(1) dictionary lookup
                    ListViewItem item = null;
                    if (listViewIndex != null && app.GroupId == _selectedGroupId)
                    {
                        listViewIndex.TryGetValue(app.Index, out item);
                    }

                    // Handle pending restart for KeepOpen apps
                    if (_pendingRestart.ContainsKey(app.Index))
                    {
                        DateTime restartTime = _pendingRestart[app.Index];
                        if (DateTime.Now >= restartTime)
                        {
                            _pendingRestart.Remove(app.Index);
                            AttemptAutoRestart(app, item);
                        }
                        // Countdown display is handled by _countdownTimer every 1s
                        continue;
                    }

                    // Use the pre-computed batch snapshot instead of per-app process query
                    ProcessManager.ProcessSnapshot snapshot;
                    if (!snapshots.TryGetValue(app.Index, out snapshot))
                    {
                        snapshot = new ProcessManager.ProcessSnapshot();
                    }
                    bool isRunning = snapshot.HasWindowedProcess;

                    // Skip watchdog checks for apps the user intentionally stopped
                    if (_pendingStop.Contains(app.Index))
                    {
                        if (!isRunning)
                        {
                            // Also clean up any lingering background processes
                            if (snapshot.HasBackgroundProcess)
                            {
                                _processManager.KillBackgroundProcesses(app);
                            }
                            _pendingStop.Remove(app.Index);
                            if (item != null)
                            {
                                item.SubItems[3].Text = "Stopped";
                                item.ForeColor = Color.Black;
                            }
                        }
                        else if (item != null)
                        {
                            item.SubItems[3].Text = "Stopping...";
                            item.ForeColor = Color.DarkOrange;
                        }
                        continue;
                    }

                    // Skip status updates for apps that have exhausted max retries
                    if (_failedApps.Contains(app.Index))
                    {
                        if (item != null)
                        {
                            item.SubItems[3].Text = "Failed";
                            item.ForeColor = Color.Red;
                        }
                        continue;
                    }

                    // Skip status updates for apps waiting in sequential Start All
                    if (_pendingSequentialStart.Contains(app.Index))
                        continue;

                    // Skip watchdog checks for apps still in their startup grace period
                    // (window may not have appeared yet)
                    if (_startGracePeriod.ContainsKey(app.Index))
                    {
                        if (DateTime.Now >= _startGracePeriod[app.Index])
                        {
                            _startGracePeriod.Remove(app.Index);
                        }
                        else
                        {
                            if (item != null)
                            {
                                if (isRunning)
                                {
                                    item.SubItems[3].Text = "Running";
                                    item.ForeColor = Color.Green;
                                }
                                else
                                {
                                    item.SubItems[3].Text = "Starting...";
                                    item.ForeColor = Color.DarkOrange;
                                }
                            }
                            continue;
                        }
                    }

                    bool wasRunning = app.IsRunning;

                    // Detect background zombie processes (no window but process still alive)
                    // This handles apps like Excel that close their window but linger in the background
                    if (!isRunning && wasRunning && snapshot.HasBackgroundProcess)
                    {
                        int killed = _processManager.KillBackgroundProcesses(app);
                        SimpleLogger.Warn("UpdateApplicationStatuses @ Form1.cs",
                            $"'{app.AppName}' lost its window but had {killed} background process(es) - killed them");

                        // After killing background processes, the state is definitively "not running"
                        // No need to re-snapshot since we just killed all background processes
                    }

                    // Detect unauthorized external launch
                    if (isRunning && !wasRunning && !_authorizedApps.Contains(app.Index))
                    {
                        HandleUnauthorizedLaunch(app, item);
                        continue;
                    }

                    // App was stopped externally (outside Instance Manager) — possible crash
                    if (!isRunning && wasRunning)
                    {
                        _authorizedApps.Remove(app.Index);
                        _notifiedUnauthorized.Remove(app.Index);

                        DateTime stopTime = DateTime.Now;

                        // If KeepOpen is enabled, treat this as a crash and schedule restart
                        if (app.KeepOpen)
                        {
                            int newCrashCount = app.CrashCount + 1;
                            int newRetryCount = app.RetryCount + 1;

                            // Check if max retries exhausted
                            if (newRetryCount >= app.MaxRetries)
                            {
                                SimpleLogger.Error("UpdateApplicationStatuses @ Form1.cs",
                                    $"'{app.AppName}' exceeded max retries ({newRetryCount}/{app.MaxRetries}). Giving up.");

                                _failedApps.Add(app.Index); // Mark as failed permanently

                                _storageService.UpdateApplicationFields(app.Index, isRunning: false,
                                    lastStop: stopTime, crashCount: newCrashCount, retryCount: newRetryCount);

                                if (item != null)
                                {
                                    item.SubItems[3].Text = "Failed";
                                    item.SubItems[5].Text = newCrashCount.ToString();
                                    item.SubItems[6].Text = newRetryCount.ToString();
                                    item.SubItems[8].Text = stopTime.ToString("yyyy-MM-dd HH:mm:ss");
                                    item.ForeColor = Color.Red;

                                    var tagApp = item.Tag as ManagedApplication;
                                    if (tagApp != null)
                                    {
                                        tagApp.CrashCount = newCrashCount;
                                        tagApp.RetryCount = newRetryCount;
                                        tagApp.LastStop = stopTime;
                                        tagApp.IsRunning = false;
                                    }
                                }
                            }
                            else
                            {
                                int delay = Math.Max(app.StartDelaySeconds, 1);
                                _pendingRestart[app.Index] = DateTime.Now.AddSeconds(delay);

                                // Start the 1-second countdown timer if not already running
                                if (!_countdownTimer.Enabled)
                                {
                                    _countdownTimer.Start();
                                }

                                SimpleLogger.Warn("UpdateApplicationStatuses @ Form1.cs",
                                    $"'{app.AppName}' crashed (KeepOpen=Yes). Crash #{newCrashCount}, retry {newRetryCount}/{app.MaxRetries}, scheduling restart in {delay}s");

                                _storageService.UpdateApplicationFields(app.Index, isRunning: false,
                                    lastStop: stopTime, crashCount: newCrashCount, retryCount: newRetryCount);

                                if (item != null)
                                {
                                    item.SubItems[3].Text = $"Restarting ({delay}s)";
                                    item.SubItems[5].Text = newCrashCount.ToString();
                                    item.SubItems[6].Text = newRetryCount.ToString();
                                    item.SubItems[8].Text = stopTime.ToString("yyyy-MM-dd HH:mm:ss");
                                    item.ForeColor = Color.DarkOrange;

                                    var tagApp = item.Tag as ManagedApplication;
                                    if (tagApp != null)
                                    {
                                        tagApp.CrashCount = newCrashCount;
                                        tagApp.RetryCount = newRetryCount;
                                        tagApp.LastStop = stopTime;
                                        tagApp.IsRunning = false;
                                    }
                                }
                            }
                        }
                        else
                        {
                            _storageService.UpdateApplicationFields(app.Index, isRunning: false, lastStop: stopTime);

                            if (item != null)
                            {
                                item.SubItems[3].Text = "Stopped";
                                item.SubItems[8].Text = stopTime.ToString("yyyy-MM-dd HH:mm:ss");
                                item.ForeColor = Color.Black;
                            }

                            SimpleLogger.Info("UpdateApplicationStatuses @ Form1.cs",
                                $"'{app.AppName}' was stopped externally");
                        }

                        continue;
                    }

                    if (app.IsRunning != isRunning)
                    {
                        _storageService.UpdateApplicationFields(app.Index, isRunning: isRunning);
                    }

                    // Keep the visible ListView row in sync with the current state
                    if (item != null)
                    {
                        string statusText = isRunning ? "Running" : "Stopped";
                        if (item.SubItems[3].Text != statusText)
                        {
                            item.SubItems[3].Text = statusText;
                            item.ForeColor = isRunning ? Color.Green : Color.Black;
                        }
                    }
                }

                // Flush any throttled storage changes periodically
                _storageService.FlushPendingChanges();
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("UpdateApplicationStatuses @ Form1.cs", $"Error updating statuses: {ex.Message}");
            }
        }

        /// <summary>
        /// Attempts to auto-restart a KeepOpen app after a crash.
        /// </summary>
        private void AttemptAutoRestart(ManagedApplication app, ListViewItem item)
        {
            try
            {
                // Double-check max retries in case settings changed while waiting
                if (app.RetryCount >= app.MaxRetries)
                {
                    SimpleLogger.Error("AttemptAutoRestart @ Form1.cs",
                        $"'{app.AppName}' exceeded max retries ({app.RetryCount}/{app.MaxRetries}). Giving up.");
                    _failedApps.Add(app.Index);
                    if (item != null)
                    {
                        item.SubItems[3].Text = "Failed";
                        item.ForeColor = Color.Red;
                    }
                    return;
                }

                if (string.IsNullOrEmpty(app.Directory) || !File.Exists(app.Directory))
                {
                    SimpleLogger.Error("AttemptAutoRestart @ Form1.cs",
                        $"Cannot auto-restart '{app.AppName}': file not found at {app.Directory ?? "(empty)"}");
                    _failedApps.Add(app.Index);
                    if (item != null)
                    {
                        item.SubItems[3].Text = "Failed";
                        item.ForeColor = Color.Red;
                    }
                    return;
                }

                if (_processManager.StartApplication(app))
                {
                    _authorizedApps.Add(app.Index);
                    _notifiedUnauthorized.Remove(app.Index);

                    // Grant a grace period so the watchdog doesn't treat the app as crashed
                    // before its main window has had time to appear
                    _startGracePeriod[app.Index] = DateTime.Now.AddSeconds(StartGracePeriodSeconds);

                    DateTime now = DateTime.Now;
                    _storageService.UpdateApplicationFields(app.Index, isRunning: true, lastStart: now, retryCount: 0);

                    if (item != null)
                    {
                        item.SubItems[3].Text = "Starting...";
                        item.SubItems[6].Text = "0";
                        item.SubItems[7].Text = now.ToString("yyyy-MM-dd HH:mm:ss");
                        item.ForeColor = Color.DarkOrange;

                        var tagApp = item.Tag as ManagedApplication;
                        if (tagApp != null)
                        {
                            tagApp.LastStart = now;
                            tagApp.IsRunning = true;
                            tagApp.RetryCount = 0;
                        }
                    }

                    SimpleLogger.Info("AttemptAutoRestart @ Form1.cs",
                        $"Auto-restarted '{app.AppName}' successfully (Retry {app.RetryCount}/{app.MaxRetries}), retry count reset to 0");
                }
                else
                {
                    SimpleLogger.Error("AttemptAutoRestart @ Form1.cs",
                        $"Failed to auto-restart '{app.AppName}' (Retry {app.RetryCount}/{app.MaxRetries})");

                    _failedApps.Add(app.Index);

                    if (item != null)
                    {
                        item.SubItems[3].Text = "Failed";
                        item.ForeColor = Color.Red;
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("AttemptAutoRestart @ Form1.cs",
                    $"Error auto-restarting '{app.AppName}': {ex.Message}");

                _failedApps.Add(app.Index);

                if (item != null)
                {
                    item.SubItems[3].Text = "Failed";
                    item.ForeColor = Color.Red;
                }
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

            // Update state through StorageService (not direct mutation)
            _storageService.UpdateApplicationFields(app.Index, isRunning: false);

            if (item != null)
            {
                item.SubItems[3].Text = "Stopped";
                item.ForeColor = Color.Black;
            }

            // Notify user only once per unauthorized attempt
            if (!_notifiedUnauthorized.Contains(app.Index))
            {
                _notifiedUnauthorized.Add(app.Index);

                int appIndex = app.Index;
                string appName = app.AppName;

                // Use BeginInvoke so the timer isn't blocked while showing the dialog
                // Guard against ObjectDisposedException if form is closing
                try
                {
                    if (!_isClosing && IsHandleCreated)
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                if (_isClosing) return;

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
                            }
                            catch (Exception ex)
                            {
                                SimpleLogger.Error("HandleUnauthorizedLaunch @ Form1.cs",
                                    $"Error showing unauthorized launch notification: {ex.Message}");
                            }
                        }));
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Form was disposed between our check and the BeginInvoke call - safe to ignore
                }
                catch (InvalidOperationException)
                {
                    // Handle was not yet created or was destroyed - safe to ignore
                }
            }
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (_selectedGroupId <= 0)
                {
                    MessageBoxHelper.ShowInfo(this, "Please select a group first.");
                    return;
                }

                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*";
                    dialog.Title = "Select Application to Manage";

                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        string appPath = dialog.FileName;
                        string appName = Path.GetFileNameWithoutExtension(appPath);

                        // Check for duplicates within the same group
                        if (_storageService.ApplicationExistsInGroup(appPath, _selectedGroupId))
                        {
                            SimpleLogger.Warn("AddButton_Click @ Form1.cs", $"Duplicate application detected in group: {appName} at {appPath}");
                            MessageBoxHelper.ShowWarning(this,
                                $"Application '{appName}' already exists in this group.\n\nPath: {appPath}");
                            return;
                        }

                        ManagedApplication app = new ManagedApplication
                        {
                            AppName = appName,
                            Directory = appPath,
                            GroupId = _selectedGroupId
                        };

                        _storageService.AddApplication(app);
                        AddApplicationToListView(app);

                        SimpleLogger.Info("AddButton_Click @ Form1.cs", $"Added application: {appName} to group {_selectedGroupId}");
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

                if (app == null)
                {
                    MessageBoxHelper.ShowError(this, "Selected item has no valid application data.");
                    return;
                }

                using (var editForm = new EditAppDialog(app))
                {
                    if (editForm.ShowDialog(this) == DialogResult.OK)
                    {
                        string newPath = editForm.NewDirectory;

                        // Check if directory changed and if the new path already exists in this group
                        if (newPath != app.Directory)
                        {
                            if (_storageService.ApplicationExistsInGroup(newPath, _selectedGroupId))
                            {
                                string newAppName = Path.GetFileNameWithoutExtension(newPath);
                                SimpleLogger.Warn("EditButton_Click @ Form1.cs", $"Duplicate application detected: {newAppName} at {newPath}");
                                MessageBoxHelper.ShowWarning(this,
                                    $"Application '{newAppName}' already exists in this group.\n\nPath: {newPath}");
                                return;
                            }

                            string oldName = app.AppName;
                            app.AppName = Path.GetFileNameWithoutExtension(newPath);
                            app.Directory = newPath;

                            selectedItem.SubItems[1].Text = app.AppName;
                            selectedItem.SubItems[2].Text = app.Directory;

                            SimpleLogger.Info("EditButton_Click @ Form1.cs", $"Edited application path: {oldName} -> {app.AppName}");
                        }

                        // If KeepOpen was turned off, cancel any pending restart
                        if (!app.KeepOpen)
                        {
                            _pendingRestart.Remove(app.Index);
                        }

                        // If retry count was reset via edit dialog, clear the failed state
                        if (app.RetryCount < app.MaxRetries)
                        {
                            _failedApps.Remove(app.Index);
                        }

                        // Update ListView display for settings columns
                        selectedItem.SubItems[4].Text = app.GetKeepOpenDisplay();
                        selectedItem.SubItems[5].Text = app.CrashCount.ToString();
                        selectedItem.SubItems[6].Text = app.RetryCount.ToString();

                        // Persist changes to storage
                        _storageService.UpdateApplication(app);

                        SimpleLogger.Info("EditButton_Click @ Form1.cs",
                            $"Updated '{app.AppName}': KeepOpen={app.KeepOpen}, StartDelay={app.StartDelaySeconds}s, StartupDelay={app.StartupDelaySeconds}s");
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

                if (app == null)
                {
                    MessageBoxHelper.ShowError(this, "Selected item has no valid application data.");
                    return;
                }

                var result = MessageBoxHelper.ShowQuestion(this,
                    $"Are you sure you want to remove '{app.AppName}' from management?\n\nNote: This will not delete the actual application file.",
                    "Confirm Delete");

                if (result == DialogResult.Yes)
                {
                    _authorizedApps.Remove(app.Index);
                    _notifiedUnauthorized.Remove(app.Index);
                    _pendingStop.Remove(app.Index);
                    _pendingRestart.Remove(app.Index);
                    _failedApps.Remove(app.Index);
                    _startGracePeriod.Remove(app.Index);
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

                if (app == null)
                {
                    MessageBoxHelper.ShowError(this, "Selected item has no valid application data.");
                    return;
                }

                // Cancel any pending restart since user is manually starting
                _pendingRestart.Remove(app.Index);

                StartSingleApplication(app, selectedItem);
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("StartButton_Click @ Form1.cs", $"Error starting application: {ex.Message}");
                MessageBoxHelper.ShowError(this, $"Error starting application: {ex.Message}");
            }
        }

        private bool StartSingleApplication(ManagedApplication app, ListViewItem item)
        {
            if (string.IsNullOrEmpty(app.Directory) || !File.Exists(app.Directory))
            {
                MessageBoxHelper.ShowError(this, $"Application file not found: {app.Directory ?? "(empty)"}");
                return false;
            }

            if (_processManager.IsApplicationRunning(app))
            {
                SimpleLogger.Info("StartSingleApplication @ Form1.cs", $"'{app.AppName}' is already running, skipping");
                return true; // Already running is not a failure
            }

            if (_processManager.StartApplication(app))
            {
                _authorizedApps.Add(app.Index);
                _notifiedUnauthorized.Remove(app.Index);
                _pendingStop.Remove(app.Index);
                _failedApps.Remove(app.Index);

                // Grant a grace period so the watchdog doesn't treat the app as crashed
                // before its main window has had time to appear
                _startGracePeriod[app.Index] = DateTime.Now.AddSeconds(StartGracePeriodSeconds);

                // Reset retry count on manual start so the app gets a fresh set of retries
                app.RetryCount = 0;
                app.LastStart = DateTime.Now;
                app.IsRunning = true;
                _storageService.UpdateApplication(app);

                if (item != null)
                {
                    item.SubItems[3].Text = "Starting...";
                    item.SubItems[6].Text = app.RetryCount.ToString();
                    item.SubItems[7].Text = app.GetLastStartDisplay();
                    item.ForeColor = Color.DarkOrange;
                }

                return true;
            }

            return false;
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

                if (app == null)
                {
                    MessageBoxHelper.ShowError(this, "Selected item has no valid application data.");
                    return;
                }

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
                    // Cancel any pending restart since user is intentionally stopping
                    _pendingRestart.Remove(app.Index);
                    StopSingleApplication(app, selectedItem);
                }
                else
                {
                    _pendingStop.Remove(app.Index);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("StopButton_Click @ Form1.cs", $"Error stopping application: {ex.Message}");
                MessageBoxHelper.ShowError(this, $"Error stopping application: {ex.Message}");
            }
        }

        private bool StopSingleApplication(ManagedApplication app, ListViewItem item)
        {
            _authorizedApps.Remove(app.Index);
            _notifiedUnauthorized.Remove(app.Index);
            _startGracePeriod.Remove(app.Index);

            // Show "Stopping" immediately so the user gets visual feedback
            if (item != null)
            {
                item.SubItems[3].Text = "Stopping...";
                item.ForeColor = Color.DarkOrange;
            }

            if (_processManager.StopApplication(app))
            {
                app.LastStop = DateTime.Now;
                app.IsRunning = false;
                _storageService.UpdateApplication(app);

                _pendingStop.Remove(app.Index);

                if (item != null)
                {
                    item.SubItems[3].Text = "Stopped";
                    item.SubItems[8].Text = app.GetLastStopDisplay();
                    item.ForeColor = Color.Black;
                }

                return true;
            }
            else
            {
                _pendingStop.Remove(app.Index);

                // Revert status if stop failed
                if (item != null)
                {
                    item.SubItems[3].Text = "Running";
                    item.ForeColor = Color.Green;
                }

                return false;
            }
        }

        private void StartAllButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (_selectedGroupId <= 0)
                {
                    MessageBoxHelper.ShowInfo(this, "Please select a group first.");
                    return;
                }

                var selectedGroup = GroupListBox.SelectedItem as ApplicationGroup;
                string groupName = selectedGroup != null ? selectedGroup.GroupName : "this group";

                var confirm = MessageBoxHelper.ShowQuestion(this,
                    $"Start all applications in '{groupName}'?",
                    "Confirm Start All");

                if (confirm != DialogResult.Yes)
                    return;

                // Collect apps to start (skip already running)
                var appsToStart = new List<KeyValuePair<ManagedApplication, ListViewItem>>();
                int alreadyRunning = 0;

                foreach (ListViewItem item in AppListView.Items)
                {
                    var app = item.Tag as ManagedApplication;
                    if (app == null) continue;

                    // Cancel any pending restart
                    _pendingRestart.Remove(app.Index);

                    if (_processManager.IsApplicationRunning(app))
                    {
                        alreadyRunning++;
                        continue;
                    }

                    appsToStart.Add(new KeyValuePair<ManagedApplication, ListViewItem>(app, item));
                }

                if (appsToStart.Count == 0)
                {
                    string noStartMsg = alreadyRunning > 0
                        ? $"All {alreadyRunning} application(s) are already running."
                        : "No applications to start.";
                    MessageBoxHelper.ShowInfo(this, noStartMsg);
                    return;
                }

                // Check if any app has a startup delay configured
                bool hasDelays = false;
                foreach (var kvp in appsToStart)
                {
                    if (kvp.Key.StartupDelaySeconds > 0)
                    {
                        hasDelays = true;
                        break;
                    }
                }

                int started = 0;
                int failed = 0;

                if (hasDelays)
                {
                    // Sequential launch with startup delays — use a timer-based approach
                    // to avoid blocking the UI thread
                    StartAllSequential(appsToStart, alreadyRunning);
                    return;
                }

                // No delays configured — launch all immediately (existing behavior)
                foreach (var kvp in appsToStart)
                {
                    if (StartSingleApplication(kvp.Key, kvp.Value))
                    {
                        started++;
                    }
                    else
                    {
                        failed++;
                    }
                }

                string msg = $"Started: {started}";
                if (alreadyRunning > 0) msg += $", Already running: {alreadyRunning}";
                if (failed > 0) msg += $", Failed: {failed}";

                SimpleLogger.Info("StartAllButton_Click @ Form1.cs", $"Start All for group {_selectedGroupId}: {msg}");

                if (failed > 0)
                    MessageBoxHelper.ShowWarning(this, msg);
                else
                    MessageBoxHelper.ShowSuccess(this, msg);
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("StartAllButton_Click @ Form1.cs", $"Error starting all: {ex.Message}");
                MessageBoxHelper.ShowError(this, $"Error starting all applications: {ex.Message}");
            }
        }

        /// <summary>
        /// Launches applications sequentially, respecting each app's StartupDelaySeconds.
        /// Uses a Timer to avoid blocking the UI thread between launches.
        /// </summary>
        private void StartAllSequential(List<KeyValuePair<ManagedApplication, ListViewItem>> appsToStart, int alreadyRunning)
        {
            int currentIndex = 0;
            int started = 0;
            int failed = 0;
            Timer sequentialTimer = null;

            // Mark all apps in the queue so the status update timer doesn't overwrite their countdown
            foreach (var kvp in appsToStart)
            {
                _pendingSequentialStart.Add(kvp.Key.Index);
            }

            // Disable Start All / Stop All buttons while sequential launch is in progress
            StartAllButton.Enabled = false;
            StopAllButton.Enabled = false;

            Action finalize = () =>
            {
                if (sequentialTimer != null)
                {
                    sequentialTimer.Stop();
                    sequentialTimer.Dispose();
                    sequentialTimer = null;
                }

                // Clear all sequential start markers
                foreach (var kvp in appsToStart)
                {
                    _pendingSequentialStart.Remove(kvp.Key.Index);
                }

                StartAllButton.Enabled = true;
                StopAllButton.Enabled = true;

                string msg = $"Started: {started}";
                if (alreadyRunning > 0) msg += $", Already running: {alreadyRunning}";
                if (failed > 0) msg += $", Failed: {failed}";

                SimpleLogger.Info("StartAllSequential @ Form1.cs", $"Start All (sequential) for group {_selectedGroupId}: {msg}");

                if (!_isClosing)
                {
                    if (failed > 0)
                        MessageBoxHelper.ShowWarning(this, msg);
                    else
                        MessageBoxHelper.ShowSuccess(this, msg);
                }
            };

            Action launchNext = null;
            launchNext = () =>
            {
                if (_isClosing || currentIndex >= appsToStart.Count)
                {
                    finalize();
                    return;
                }

                var kvp = appsToStart[currentIndex];
                var app = kvp.Key;
                var item = kvp.Value;

                // Remove from sequential tracking before launch so watchdog can monitor it
                _pendingSequentialStart.Remove(app.Index);

                // Launch the current app
                if (StartSingleApplication(app, item))
                {
                    started++;
                    SimpleLogger.Info("StartAllSequential @ Form1.cs",
                        $"Started '{app.AppName}' ({currentIndex + 1}/{appsToStart.Count})");
                }
                else
                {
                    failed++;
                    SimpleLogger.Error("StartAllSequential @ Form1.cs",
                        $"Failed to start '{app.AppName}' ({currentIndex + 1}/{appsToStart.Count})");
                }

                currentIndex++;

                // If there are more apps, check if the next one has a startup delay
                if (currentIndex < appsToStart.Count)
                {
                    int nextDelay = appsToStart[currentIndex].Key.StartupDelaySeconds;
                    if (nextDelay > 0)
                    {
                        // Show countdown on the next app's status
                        var nextItem = appsToStart[currentIndex].Value;
                        if (nextItem != null)
                        {
                            nextItem.SubItems[3].Text = $"Waiting ({nextDelay}s)";
                            nextItem.ForeColor = Color.DarkOrange;
                        }

                        // Set up a countdown timer
                        int secondsLeft = nextDelay;

                        // Dispose previous timer if any
                        if (sequentialTimer != null)
                        {
                            sequentialTimer.Stop();
                            sequentialTimer.Dispose();
                        }

                        sequentialTimer = new Timer();
                        sequentialTimer.Interval = 1000;
                        sequentialTimer.Tick += (s, ev) =>
                        {
                            secondsLeft--;
                            if (secondsLeft <= 0 || _isClosing)
                            {
                                if (sequentialTimer != null)
                                {
                                    sequentialTimer.Stop();
                                }
                                launchNext();
                            }
                            else if (nextItem != null)
                            {
                                nextItem.SubItems[3].Text = $"Waiting ({secondsLeft}s)";
                            }
                        };
                        sequentialTimer.Start();
                    }
                    else
                    {
                        // No delay — launch immediately
                        launchNext();
                    }
                }
                else
                {
                    // No more apps — finalize
                    finalize();
                }
            };

            // Start the first app (the first app launches immediately, delays apply to subsequent apps)
            launchNext();
        }

        private void StopAllButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (_selectedGroupId <= 0)
                {
                    MessageBoxHelper.ShowInfo(this, "Please select a group first.");
                    return;
                }

                var selectedGroup = GroupListBox.SelectedItem as ApplicationGroup;
                string groupName = selectedGroup != null ? selectedGroup.GroupName : "this group";

                var confirm = MessageBoxHelper.ShowQuestion(this,
                    $"Stop all running applications in '{groupName}'?",
                    "Confirm Stop All");

                if (confirm != DialogResult.Yes)
                    return;

                int stopped = 0;
                int failed = 0;
                int notRunning = 0;

                for (int i = AppListView.Items.Count - 1; i >= 0; i--)
                {
                    ListViewItem item = AppListView.Items[i];
                    var app = item.Tag as ManagedApplication;
                    if (app == null) continue;

                    // Cancel any pending restart since user is intentionally stopping all
                    _pendingRestart.Remove(app.Index);
                    _failedApps.Remove(app.Index);

                    if (_processManager.IsApplicationRunning(app))
                    {
                        if (StopSingleApplication(app, item))
                        {
                            stopped++;
                        }
                        else
                        {
                            failed++;
                        }
                    }
                    else
                    {
                        notRunning++;
                    }
                }

                string msg = $"Stopped: {stopped}";
                if (notRunning > 0) msg += $", Not running: {notRunning}";
                if (failed > 0) msg += $", Failed: {failed}";

                SimpleLogger.Info("StopAllButton_Click @ Form1.cs", $"Stop All for group {_selectedGroupId}: {msg}");

                if (failed > 0)
                    MessageBoxHelper.ShowWarning(this, msg);
                else
                    MessageBoxHelper.ShowSuccess(this, msg);
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("StopAllButton_Click @ Form1.cs", $"Error stopping all: {ex.Message}");
                MessageBoxHelper.ShowError(this, $"Error stopping all applications: {ex.Message}");
            }
        }

        private void LogsButton_Click(object sender, EventArgs e)
        {
            try
            {
                string logDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

                if (System.IO.Directory.Exists(logDirectory))
                {
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + logDirectory + "\"");
                    SimpleLogger.Info("LogsButton_Click @ Form1.cs", $"Opened logs folder: {logDirectory}");
                }
                else
                {
                    MessageBoxHelper.ShowWarning(this, $"Logs folder not found:\n\n{logDirectory}");
                    SimpleLogger.Warn("LogsButton_Click @ Form1.cs", $"Logs folder does not exist: {logDirectory}");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("LogsButton_Click @ Form1.cs", $"Error opening logs folder: {ex.Message}");
                MessageBoxHelper.ShowError(this, $"Error opening logs folder: {ex.Message}");
            }
        }

        private void UpdateStationNameDisplay()
        {
            string stationName = _settingsService.StationName;
            if (!string.IsNullOrEmpty(stationName))
            {
                AppSubtitle.Text = $"- {stationName}";
                AppSubtitle.Visible = true;
            }
            else
            {
                AppSubtitle.Text = "";
                AppSubtitle.Visible = false;
            }
        }

        private void SettingsButton_Click(object sender, EventArgs e)
        {
            try
            {
                string stationName;
                var result = InputDialog.Show(this, "Settings", "Enter station name:", _settingsService.StationName, out stationName);

                if (result == DialogResult.OK)
                {
                    _settingsService.StationName = stationName;
                    UpdateStationNameDisplay();
                    SimpleLogger.Info("SettingsButton_Click @ Form1.cs", $"Station name updated to: {stationName}");
                    MessageBoxHelper.ShowSuccess(this, "Station name updated successfully!");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("SettingsButton_Click @ Form1.cs", $"Error updating settings: {ex.Message}");
                MessageBoxHelper.ShowError(this, $"Error updating settings: {ex.Message}");
            }
        }

        private void UserGuideButton_Click(object sender, EventArgs e)
        {
            try
            {
                string guidePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "USER_GUIDE.md");

                if (File.Exists(guidePath))
                {
                    System.Diagnostics.Process.Start(guidePath);
                    SimpleLogger.Info("UserGuideButton_Click @ Form1.cs", $"Opened user guide: {guidePath}");
                }
                else
                {
                    MessageBoxHelper.ShowWarning(this, $"User guide not found:\n\n{guidePath}");
                    SimpleLogger.Warn("UserGuideButton_Click @ Form1.cs", $"User guide does not exist: {guidePath}");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("UserGuideButton_Click @ Form1.cs", $"Error opening user guide: {ex.Message}");
                MessageBoxHelper.ShowError(this, $"Error opening user guide: {ex.Message}");
            }
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            _statusUpdateTimer.Stop();
            try
            {
                LoadGroups();

                // Re-select the previously selected group if it still exists
                if (_selectedGroupId > 0)
                {
                    bool found = false;
                    for (int i = 0; i < GroupListBox.Items.Count; i++)
                    {
                        var group = GroupListBox.Items[i] as ApplicationGroup;
                        if (group != null && group.GroupId == _selectedGroupId)
                        {
                            GroupListBox.SelectedIndex = i;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        _selectedGroupId = -1;
                        SelectedGroupLabel.Text = "Select a group to manage applications";
                        AppListView.Items.Clear();
                        UpdateAppButtonsEnabled();
                    }
                }

                MessageBoxHelper.ShowSuccess(this, "Refreshed!");
            }
            finally
            {
                _statusUpdateTimer.Start();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _isClosing = true;

            if (_statusUpdateTimer != null)
            {
                _statusUpdateTimer.Stop();
                _statusUpdateTimer.Dispose();
                _statusUpdateTimer = null;
            }

            if (_countdownTimer != null)
            {
                _countdownTimer.Stop();
                _countdownTimer.Dispose();
                _countdownTimer = null;
            }

            // Flush any pending data before closing
            if (_storageService != null)
            {
                _storageService.FlushPendingChanges();
            }

            base.OnFormClosing(e);

            SimpleLogger.Info("OnFormClosing @ Form1.cs", "Application closing");
            SimpleLogger.Flush();
        }
    }
}
