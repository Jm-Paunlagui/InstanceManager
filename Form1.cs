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

        // Tracks which apps were started/authorized through IMEE (or were running at startup)
        private HashSet<int> _authorizedApps = new HashSet<int>();
        // Prevents duplicate notifications for the same unauthorized launch
        private HashSet<int> _notifiedUnauthorized = new HashSet<int>();
        // Prevents duplicate health notifications when monitoring is disabled
        private HashSet<int> _notifiedHealthIssues = new HashSet<int>();
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
        // Prevents duplicate notifications for apps that exhausted max retries
        private HashSet<int> _notifiedFailedApps = new HashSet<int>();
        // Tracks apps that are pending sequential startup launch (Start All with delays)
        // so the status update timer doesn't overwrite their countdown text
        private HashSet<int> _pendingSequentialStart = new HashSet<int>();
        // Tracks the scheduled launch time for each pending sequential app so countdown can be displayed
        private Dictionary<int, DateTime> _pendingSequentialStartAt = new Dictionary<int, DateTime>();
        // Tracks the sequential launch timer so it can be disposed on form close
        private Timer _sequentialLaunchTimer;
        // Tracks apps that were recently started and are in a grace period
        // to allow the process window to appear before watchdog monitoring begins
        private Dictionary<int, DateTime> _startGracePeriod = new Dictionary<int, DateTime>();
        // Tracks auto-restarted apps that need to run for a stable period before retry count resets
        // Key: app Index, Value: DateTime when the app was confirmed as Running after auto-restart
        private Dictionary<int, DateTime> _stableRunCheck = new Dictionary<int, DateTime>();
        // Tracks apps whose main window is not responding (possible crash dialog)
        // Key: app Index, Value: DateTime when not-responding was first detected
        private Dictionary<int, DateTime> _notRespondingTracking = new Dictionary<int, DateTime>();
        // Dedicated 1-second timer for smooth restart countdown display
        private Timer _countdownTimer;
        // Cached group status for owner-drawn group list indicators
        private Dictionary<int, GroupDisplayStatus> _groupDisplayStatus = new Dictionary<int, GroupDisplayStatus>();
        // Reusable ListView index to avoid per-tick allocation
        private Dictionary<int, ListViewItem> _listViewIndex = new Dictionary<int, ListViewItem>();
        // Tracks last time a periodic GC was triggered to prevent memory growth over 24/7 operation
        private DateTime _lastGcCollect = DateTime.Now;
        // Tracks CPU time samples for detecting CPU-hung processes
        // Key: app Index, Value: (lastSampleTime, lastTotalCpuTime)
        private Dictionary<int, KeyValuePair<DateTime, TimeSpan>> _cpuTimeSamples = new Dictionary<int, KeyValuePair<DateTime, TimeSpan>>();
        // Tracks consecutive high-CPU detections to avoid false positives from brief spikes
        // Key: app Index, Value: number of consecutive high-CPU ticks
        private Dictionary<int, int> _highCpuStreak = new Dictionary<int, int>();
        // Number of consecutive high-CPU ticks before treating a process as hung
        private const int HighCpuStreakThreshold = 3;
        // CPU usage threshold (0.0 - 1.0) above which a process is considered CPU-hung
        private const double HighCpuThreshold = 0.95;
        // Tracks apps whose window title matched an error dialog pattern
        // Key: app Index, Value: DateTime when first detected
        private Dictionary<int, DateTime> _errorDialogTracking = new Dictionary<int, DateTime>();
        // Tracks the established (initial) window title for each running app.
        // Used to detect unexpected title changes that may indicate an error dialog overlay.
        // Key: app Index, Value: the window title captured after the grace period
        private Dictionary<int, string> _knownWindowTitles = new Dictionary<int, string>();
        // Tracks apps whose window title changed unexpectedly (possible error dialog).
        // Key: app Index, Value: DateTime when first detected (requires 2 consecutive ticks)
        private Dictionary<int, DateTime> _titleChangeTracking = new Dictionary<int, DateTime>();
        // Tracks apps that were force-killed by health monitoring, storing the original
        // health issue reason. When the !isRunning && wasRunning branch runs on the next tick,
        // it uses this reason instead of classifying the forced termination's exit code
        // (which would be misleading, e.g. "General error (exit code 1)" from TerminateProcess).
        private Dictionary<int, string> _forceKillReason = new Dictionary<int, string>();
        // Cached StringFormat objects for owner-drawn GroupListBox to avoid per-draw GDI allocations
        private readonly StringFormat _groupNameFormat = new StringFormat
        {
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter
        };
        private readonly StringFormat _groupSuffixFormat = new StringFormat
        {
            LineAlignment = StringAlignment.Center,
            Alignment = StringAlignment.Far,
            FormatFlags = StringFormatFlags.NoWrap
        };

        /// <summary>
        /// Cached rendering state for a single group row in the GroupListBox.
        /// Compared by value to avoid redundant repaints.
        /// </summary>
        private struct GroupDisplayStatus
        {
            public Color IndicatorColor;
            public string Suffix; // e.g. "2 / 5" or "" (empty = no suffix)

            public bool Equals(GroupDisplayStatus other)
            {
                return IndicatorColor == other.IndicatorColor && Suffix == other.Suffix;
            }
        }

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

        private void Main_Shown(object sender, EventArgs e)
        {
            if (!_settingsService.IsServerMode || _settingsService.ServerAutoRunMode == 0)
                return;

            try
            {
                List<ApplicationGroup> groupsToStart;
                if (_settingsService.ServerAutoRunMode == 1)
                {
                    groupsToStart = _storageService.GetAllGroups();
                }
                else
                {
                    var selectedIds = new HashSet<int>(_settingsService.ServerAutoRunGroupIdList);
                    groupsToStart = _storageService.GetAllGroups()
                        .FindAll(g => selectedIds.Contains(g.GroupId));
                }

                // Collect all apps to start across all configured groups (in SortOrder)
                var appsToStart = new List<ManagedApplication>();
                foreach (var group in groupsToStart)
                {
                    foreach (var app in _storageService.GetApplicationsByGroup(group.GroupId))
                    {
                        if (!string.IsNullOrEmpty(app.Directory) &&
                            File.Exists(app.Directory) &&
                            !_processManager.IsApplicationRunning(app))
                        {
                            appsToStart.Add(app);
                        }
                    }
                }

                SimpleLogger.Info("Main_Shown @ Form1.cs",
                    $"Server mode: auto-starting {appsToStart.Count} app(s) across {groupsToStart.Count} group(s)");

                if (appsToStart.Count > 0)
                    ServerStartSequential(appsToStart);
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("Main_Shown @ Form1.cs", $"Server auto-start error: {ex.Message}");
            }
        }

        /// <summary>
        /// Launches apps sequentially for server auto-start, respecting each app's StartupDelaySeconds.
        /// Uses 1-second tick timers to show live countdowns in the ListView, matching StartAllSequential.
        /// </summary>
        private void ServerStartSequential(List<ManagedApplication> appsToStart)
        {
            int currentIndex = 0;

            // Mark all queued apps so the watchdog doesn't overwrite their countdown text
            foreach (var app in appsToStart)
                _pendingSequentialStart.Add(app.Index);

            Action<ManagedApplication, int> beginCountdown = null;
            Action launchNext = null;

            beginCountdown = (targetApp, delaySeconds) =>
            {
                _pendingSequentialStartAt[targetApp.Index] = DateTime.Now.AddSeconds(delaySeconds);
                int secondsLeft = delaySeconds;

                // Show initial countdown on the app's ListView item if it's currently visible
                ListViewItem targetItem;
                if (_listViewIndex.TryGetValue(targetApp.Index, out targetItem) && targetItem != null)
                {
                    targetItem.SubItems[3].Text = $"Waiting ({secondsLeft}s)";
                    targetItem.ForeColor = Color.DarkOrange;
                }

                if (_sequentialLaunchTimer != null)
                {
                    _sequentialLaunchTimer.Stop();
                    _sequentialLaunchTimer.Dispose();
                }

                _sequentialLaunchTimer = new Timer { Interval = 1000 };
                ManagedApplication capturedApp = targetApp;
                _sequentialLaunchTimer.Tick += (s, ev) =>
                {
                    secondsLeft--;
                    if (secondsLeft <= 0 || _isClosing)
                    {
                        if (_sequentialLaunchTimer != null)
                            _sequentialLaunchTimer.Stop();
                        launchNext();
                    }
                    else
                    {
                        ListViewItem tickItem;
                        if (_listViewIndex.TryGetValue(capturedApp.Index, out tickItem) && tickItem != null)
                            tickItem.SubItems[3].Text = $"Waiting ({secondsLeft}s)";
                    }
                };
                _sequentialLaunchTimer.Start();
            };

            launchNext = () =>
            {
                if (_isClosing || currentIndex >= appsToStart.Count)
                {
                    if (_sequentialLaunchTimer != null)
                    {
                        _sequentialLaunchTimer.Stop();
                        _sequentialLaunchTimer.Dispose();
                        _sequentialLaunchTimer = null;
                    }
                    return;
                }

                var app = appsToStart[currentIndex];
                _pendingSequentialStart.Remove(app.Index);
                _pendingSequentialStartAt.Remove(app.Index);

                // Launch the current app
                if (_processManager.StartApplication(app))
                {
                    _authorizedApps.Add(app.Index);
                    _notifiedUnauthorized.Remove(app.Index);
                    _notifiedHealthIssues.Remove(app.Index);
                    _pendingStop.Remove(app.Index);
                    _failedApps.Remove(app.Index);
                    _forceKillReason.Remove(app.Index);
                    _startGracePeriod[app.Index] = DateTime.Now.AddSeconds(_settingsService.StartGracePeriodSeconds);
                    int stablePeriod = Math.Max(app.StableRunPeriodSeconds, 5);
                    _stableRunCheck[app.Index] = DateTime.Now.AddSeconds(stablePeriod);
                    _storageService.UpdateApplicationFields(app.Index, isRunning: true, lastStart: DateTime.Now);

                    // Update the launched app's ListView item immediately
                    ListViewItem launchedItem;
                    if (_listViewIndex.TryGetValue(app.Index, out launchedItem) && launchedItem != null)
                    {
                        launchedItem.SubItems[3].Text = "Starting...";
                        launchedItem.SubItems[7].Text = app.GetLastStartDisplay();
                        launchedItem.ForeColor = Color.DarkOrange;
                    }

                    SimpleLogger.Info("ServerStartSequential @ Form1.cs",
                        $"Server auto-started '{app.AppName}' ({currentIndex + 1}/{appsToStart.Count})");
                }
                else
                {
                    SimpleLogger.Error("ServerStartSequential @ Form1.cs",
                        $"Failed to auto-start '{app.AppName}' ({currentIndex + 1}/{appsToStart.Count})");
                }

                currentIndex++;

                if (currentIndex < appsToStart.Count)
                {
                    int nextDelay = appsToStart[currentIndex].StartupDelaySeconds;
                    if (nextDelay > 0)
                        beginCountdown(appsToStart[currentIndex], nextDelay);
                    else
                        launchNext();
                }
                else
                {
                    if (_sequentialLaunchTimer != null)
                    {
                        _sequentialLaunchTimer.Stop();
                        _sequentialLaunchTimer.Dispose();
                        _sequentialLaunchTimer = null;
                    }
                }
            };

            // Kick off: delay first app if it has StartupDelaySeconds configured, otherwise launch immediately
            int firstDelay = appsToStart[0].StartupDelaySeconds;
            if (firstDelay > 0)
                beginCountdown(appsToStart[0], firstDelay);
            else
                launchNext();
        }

        private void InitializeServices()
        {
            _storageService = new StorageService();
            _processManager = new ProcessManager();
            _settingsService = new SettingsService();

            // Apply configurable settings to services
            ApplySettingsToServices();
        }

        /// <summary>
        /// Applies current settings from SettingsService to all dependent services and timers.
        /// </summary>
        private void ApplySettingsToServices()
        {
            _storageService.SetSaveInterval(_settingsService.StorageSaveIntervalSeconds);
            SimpleLogger.Configure(
                _settingsService.LogFlushIntervalSeconds,
                _settingsService.LogBufferSize,
                _settingsService.LogRetentionDays);

            // Sync the Windows startup registry entry with the current setting
            StartupManager.SetRunOnStartup(_settingsService.RunOnStartup);
        }

        /// <summary>
        /// On startup, terminate any managed apps that were already running
        /// since they were not launched through IMEE.
        /// Also resets stale runtime state (IsRunning, RetryCount) for all apps
        /// to prevent false crash detections and accumulated retry counts from previous sessions.
        /// </summary>
        private void TerminateAlreadyRunningApps()
        {
            try
            {
                var apps = _storageService.GetAllApplicationsReadOnly();
                List<string> terminatedApps = new List<string>();
                // Track process names already terminated to avoid duplicate kills
                // when the same executable appears in multiple groups
                HashSet<string> terminatedProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                        string processName = Path.GetFileNameWithoutExtension(app.Directory);

                        // Only terminate once per process name (same exe in multiple groups)
                        if (!terminatedProcessNames.Contains(processName))
                        {
                            SimpleLogger.Warn("TerminateAlreadyRunningApps @ Form1.cs",
                                $"'{app.AppName}' was running before IMEE started - terminating")

                            ;

                            _processManager.StopApplication(app);
                            terminatedProcessNames.Add(processName);
                            terminatedApps.Add(app.AppName ?? "(unknown)");
                        }

                        _storageService.UpdateApplicationFields(app.Index, isRunning: false, lastStop: DateTime.Now, retryCount: 0);
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
                        $"The following application(s) were running before IMEE started and have been terminated:\n\n" +
                        $"- {appList}\n\n" +
                        "All managed applications must be started through IMEE.");
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
            _statusUpdateTimer.Interval = _settingsService.StatusPollIntervalMs;
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

                // Periodic GC to prevent long-term memory growth during 24/7 operation
                if ((DateTime.Now - _lastGcCollect).TotalMinutes >= _settingsService.GcCollectIntervalMinutes)
                {
                    _lastGcCollect = DateTime.Now;
                    GC.Collect(1, GCCollectionMode.Optimized);
                }
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
                    listViewIndex = _listViewIndex;
                    RebuildListViewIndex(listViewIndex);
                }

                // Snapshot the keys to avoid modifying the dictionary during iteration.
                // _pendingRestart is typically small (crashed apps only), so this is cheap.
                var keys = new List<int>(_pendingRestart.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    int appIndex = keys[i];
                    DateTime restartTime;
                    if (!_pendingRestart.TryGetValue(appIndex, out restartTime))
                        continue;

                    ListViewItem item = null;
                    if (listViewIndex != null)
                    {
                        listViewIndex.TryGetValue(appIndex, out item);
                    }

                    if (item != null)
                    {
                        int secondsLeft = (int)Math.Ceiling((restartTime - DateTime.Now).TotalSeconds);
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

                    string oldName = selectedGroup.GroupName;

                    // Check if the name actually changed
                    if (oldName == newName)
                    {
                        SimpleLogger.Info("EditGroupButton_Click @ Form1.cs",
                            $"Group edit dialog closed with OK � no name change for '{oldName}' (GroupId: {selectedGroup.GroupId})");
                        return;
                    }

                    if (_storageService.GroupNameExists(newName, selectedGroup.GroupId))
                    {
                        MessageBoxHelper.ShowWarning(this, $"A group named '{newName}' already exists.");
                        return;
                    }

                    selectedGroup.GroupName = newName;
                    _storageService.UpdateGroup(selectedGroup);

                    // Refresh the listbox display
                    int idx = GroupListBox.SelectedIndex;
                    GroupListBox.Items[idx] = selectedGroup;
                    SelectedGroupLabel.Text = $"Group: {selectedGroup.GroupName}";

                    SimpleLogger.Info("GroupRenamed @ Form1.cs",
                        $"Group renamed: \"{oldName}\" to \"{newName}\" (GroupId: {selectedGroup.GroupId})");
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
                        _notifiedHealthIssues.Remove(app.Index);
                        _pendingStop.Remove(app.Index);
                        _pendingRestart.Remove(app.Index);
                        _failedApps.Remove(app.Index);
                        _startGracePeriod.Remove(app.Index);
                        _stableRunCheck.Remove(app.Index);
                        _notRespondingTracking.Remove(app.Index);
                        _cpuTimeSamples.Remove(app.Index);
                        _highCpuStreak.Remove(app.Index);
                        _errorDialogTracking.Remove(app.Index);
                        _knownWindowTitles.Remove(app.Index);
                        _titleChangeTracking.Remove(app.Index);
                        _forceKillReason.Remove(app.Index);
                    }

                    string groupName = selectedGroup.GroupName;
                    _groupDisplayStatus.Remove(selectedGroup.GroupId);
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

                // Update group indicators after loading apps (their IsRunning state is now current)
                UpdateGroupIndicators(_storageService.GetAllApplicationsReadOnly());

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
            bool isCrossGroupRun = false;
            if (!string.IsNullOrEmpty(app.Directory))
            {
                isRunning = _processManager.IsApplicationRunning(app);

                // If the process is running but this entry didn't start it,
                // check if a sibling entry in another group is authorized
                if (isRunning && !_authorizedApps.Contains(app.Index))
                {
                    isCrossGroupRun = _storageService.FindAuthorizedSibling(app.Directory, app.Index, _authorizedApps) != null;
                }
            }

            // Cross-group running apps should not be marked as IsRunning for this entry
            app.IsRunning = isRunning && !isCrossGroupRun;

            string statusText;
            Color statusColor;
            if (isCrossGroupRun)
            {
                statusText = "Running (Other Group)";
                statusColor = Color.DarkCyan;
            }
            else if (isRunning)
            {
                if (_startGracePeriod.ContainsKey(app.Index))
                {
                    statusText = "Starting...";
                    statusColor = Color.DarkOrange;
                }
                else
                {
                    statusText = "Running";
                    statusColor = Color.Green;
                }
            }
            else if (_failedApps.Contains(app.Index))
            {
                statusText = "Failed";
                statusColor = Color.Red;
            }
            else if (_pendingRestart.ContainsKey(app.Index))
            {
                int secondsLeft = (int)Math.Ceiling((_pendingRestart[app.Index] - DateTime.Now).TotalSeconds);
                statusText = secondsLeft > 0 ? $"Restarting ({secondsLeft}s)" : "Starting...";
                statusColor = Color.DarkOrange;
            }
            else if (_pendingStop.Contains(app.Index))
            {
                statusText = "Stopping...";
                statusColor = Color.DarkOrange;
            }
            else if (_pendingSequentialStart.Contains(app.Index))
            {
                DateTime target;
                if (_pendingSequentialStartAt.TryGetValue(app.Index, out target))
                {
                    int sLeft = (int)Math.Ceiling((target - DateTime.Now).TotalSeconds);
                    statusText = sLeft > 0 ? $"Waiting ({sLeft}s)" : "Waiting...";
                }
                else
                {
                    statusText = "Waiting...";
                }
                statusColor = Color.DarkOrange;
            }
            else
            {
                // Determine stopped sub-status from the app's last known state
                if (app.CrashCount > 0 && app.LastStop.HasValue
                    && (!app.LastStart.HasValue || app.LastStop.Value > app.LastStart.Value) && app.LastExitCode.HasValue && app.LastExitCode.Value != 0)
                {
                    statusText = "Stopped (Crashed)";
                    statusColor = Color.DarkOrange;
                }
                else
                {
                    statusText = "Stopped";
                    statusColor = Color.Black;
                }
            }

            ListViewItem item = new ListViewItem(app.Index.ToString());
            item.SubItems.Add(app.AppName ?? "");                    // [1] Application
            item.SubItems.Add(app.Directory ?? "");                  // [2] Directory
            item.SubItems.Add(statusText);                           // [3] Status
            item.SubItems.Add(app.GetKeepOpenDisplay());             // [4] Keep Open
            item.SubItems.Add(app.CrashCount.ToString());            // [5] Crashes
            item.SubItems.Add(app.RetryCount.ToString());            // [6] Retries
            item.SubItems.Add(app.GetLastStartDisplay());            // [7] Last Start
            item.SubItems.Add(app.GetLastStopDisplay());             // [8] Last Stop
            item.SubItems.Add(FormatExitCodeDisplay(app.LastExitCode)); // [9] Exit Code
            item.Tag = app;
            item.ForeColor = statusColor;

            AppListView.Items.Add(item);
        }

        private void UpdateApplicationStatuses()
        {
            try
            {
                // Update statuses for ALL managed apps (not just the visible group)
                // to enforce watchdog across all groups
                var allApps = _storageService.GetAllApplicationsReadOnly();

                // Single process enumeration for ALL apps � replaces N individual
                // GetProcessesByName calls with one Process.GetProcesses() call
                var snapshots = _processManager.GetBatchProcessSnapshot(allApps);

                // Build an index of ListView items by app Index for O(1) lookup
                // instead of O(n) linear scan per app
                Dictionary<int, ListViewItem> listViewIndex = null;
                if (_selectedGroupId > 0 && AppListView.Items.Count > 0)
                {
                    listViewIndex = _listViewIndex;
                    RebuildListViewIndex(listViewIndex);
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
                            _stableRunCheck.Remove(app.Index);
                            _notRespondingTracking.Remove(app.Index);
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

                    // Keep countdown display current for apps waiting in sequential start queue
                    if (_pendingSequentialStart.Contains(app.Index))
                    {
                        if (item != null)
                        {
                            DateTime target;
                            if (_pendingSequentialStartAt.TryGetValue(app.Index, out target))
                            {
                                int sLeft = (int)Math.Ceiling((target - DateTime.Now).TotalSeconds);
                                item.SubItems[3].Text = sLeft > 0 ? $"Waiting ({sLeft}s)" : "Waiting...";
                            }
                            else
                            {
                                item.SubItems[3].Text = "Waiting...";
                            }
                            item.ForeColor = Color.DarkOrange;
                        }
                        continue;
                    }

                    // Skip watchdog checks for apps still in their startup grace period
                    // (window may not have appeared yet)
                    if (_startGracePeriod.ContainsKey(app.Index))
                    {
                        // If the app window appeared during the grace period, track the process handle
                        // and update the display but keep the grace period active until it expires.
                        if (isRunning && snapshot.FirstWindowedPid > 0)
                        {
                            SimpleLogger.Info("UpdateApplicationStatuses @ Form1.cs",
                                $"'{app.AppName}' window detected (App PID: {snapshot.FirstWindowedPid}) - grace period continues");

                            _processManager.TryTrackActualAppProcess(app);
                            // Do NOT remove the grace period yet. WinForms apps have a brief window
                            // between "main window handle created" and "message pump started" where
                            // Process.Responding returns false. If health monitoring fires immediately
                            // it will send CloseMainWindow (triggering the app's exit dialog) then
                            // Kill (exit code 0xFFFFFFFF). With KeepOpen this causes an infinite loop.
                            // Let the grace period run its full configured duration before health checks begin.

                            if (item != null)
                            {
                                item.SubItems[3].Text = "Running";
                                item.ForeColor = Color.Green;
                            }
                            continue;
                        }
                        else if (DateTime.Now >= _startGracePeriod[app.Index])
                        {
                            // Grace period expired � check if the app is now running
                            if (isRunning && snapshot.FirstWindowedPid > 0)
                            {
                                SimpleLogger.Info("UpdateApplicationStatuses @ Form1.cs",
                                    $"'{app.AppName}' is now running (App PID: {snapshot.FirstWindowedPid})");

                                // Ensure we have a tracked Process handle for the actual app
                                // (important for launcher-started apps where the handle may not
                                // have been available at launch time)
                                _processManager.TryTrackActualAppProcess(app);
                            }
                            _startGracePeriod.Remove(app.Index);

                            // Update ListView to reflect the app is now running (or stopped if it exited during grace)
                            if (item != null)
                            {
                                if (isRunning)
                                {
                                    item.SubItems[3].Text = "Running";
                                    item.ForeColor = Color.Green;
                                }
                                else
                                {
                                    item.SubItems[3].Text = "Stopped";
                                    item.ForeColor = Color.Black;
                                }
                            }

                            // IMPORTANT: If the app is NOT running when the grace period expires,
                            // do NOT fall through to the crash detection branch (!isRunning && wasRunning).
                            // For launcher-based apps, the actual application process may still be
                            // starting (the launcher exits quickly but the app takes time to show its window).
                            // Falling through would falsely remove the app from _authorizedApps and
                            // trigger unauthorized launch detection on the next tick when the app finally appears.
                            if (!isRunning)
                            {
                                // For apps with a launcher, extend the grace period by one more poll cycle
                                // to give the launched application more time to show its window.
                                int extensionSeconds = Math.Max(_settingsService.StatusPollIntervalMs / 1000, 5);
                                _startGracePeriod[app.Index] = DateTime.Now.AddSeconds(extensionSeconds);

                                SimpleLogger.Info("UpdateApplicationStatuses @ Form1.cs",
                                    $"'{app.AppName}' not yet running after grace period (launcher-based app) � extending grace period by {extensionSeconds}s");

                                if (item != null)
                                {
                                    item.SubItems[3].Text = "Starting...";
                                    item.ForeColor = Color.DarkOrange;
                                }
                            }
                            else
                            {
                                // App is running when grace period expires — fall through to normal monitoring
                                SimpleLogger.Info("UpdateApplicationStatuses @ Form1.cs", $"'{app.AppName}' grace period expired, app running");
                            }
                        }
                        else
                        {
                            if (item != null)
                            {
                                // Grace period active � app was just launched, waiting for window
                                item.SubItems[3].Text = "Starting...";
                                item.ForeColor = Color.DarkOrange;
                            }
                            continue;
                        }
                    }

                    bool wasRunning = app.IsRunning;

                    // Check stable run period: if an auto-restarted app has been running
                    // long enough, reset its retry count to 0
                    if (isRunning && _stableRunCheck.ContainsKey(app.Index))
                    {
                        if (DateTime.Now >= _stableRunCheck[app.Index])
                        {
                            _stableRunCheck.Remove(app.Index);

                            if (app.RetryCount > 0)
                            {
                                SimpleLogger.Info("UpdateApplicationStatuses @ Form1.cs",
                                    $"'{app.AppName}' has been running stably � resetting retry count from {app.RetryCount} to 0");

                                _storageService.UpdateApplicationFields(app.Index, retryCount: 0, clearLastExitCode: true);

                                if (item != null)
                                {
                                    item.SubItems[6].Text = "0";
                                    item.SubItems[9].Text = "�";

                                    var tagApp = item.Tag as ManagedApplication;
                                    if (tagApp != null)
                                    {
                                        tagApp.RetryCount = 0;
                                        tagApp.LastExitCode = null;
                                    }
                                }
                            }
                        }
                    }

                    // Detect not-responding KeepOpen apps (e.g. unhandled exception dialog)
                    // If the app has a window but is not responding, it may be stuck on a crash dialog.
                    // Force-kill it so auto-restart can take over.
                    // Uses configurable NotRespondingTimeoutSeconds per app, or falls back to
                    // 2 consecutive poll cycles if not configured (timeout = 0).
                    if (isRunning && wasRunning && snapshot.HasNotRespondingProcess)
                    {
                        if (!_notRespondingTracking.ContainsKey(app.Index))
                        {
                            _notRespondingTracking[app.Index] = DateTime.Now;

                            // Only log once per issue � _notifiedHealthIssues prevents re-logging
                            // when the not-responding state persists after NotifyHealthIssue was called
                            if (!_notifiedHealthIssues.Contains(app.Index))
                            {
                                SimpleLogger.Warn("UpdateApplicationStatuses @ Form1.cs",
                                    $"'{app.AppName}' is not responding � monitoring for timeout");
                            }
                        }
                        else
                        {
                            int timeoutSeconds = app.NotRespondingTimeoutSeconds;
                            bool shouldKill;

                            if (timeoutSeconds > 0)
                            {
                                // Time-based timeout: kill after the configured duration
                                double elapsed = (DateTime.Now - _notRespondingTracking[app.Index]).TotalSeconds;
                                shouldKill = elapsed >= timeoutSeconds;

                                if (!shouldKill)
                                {
                                    // Throttle debug log: only log every 5 seconds to avoid flooding
                                    if ((int)elapsed % 5 == 0)
                                    {
                                        SimpleLogger.Debug("UpdateApplicationStatuses @ Form1.cs",
                                            $"'{app.AppName}' still not responding ({(int)elapsed}/{timeoutSeconds}s)");
                                    }
                                }
                            }
                            else
                            {
                                // Default: 2 consecutive poll cycles (backward compatible)
                                shouldKill = true;
                            }

                            if (shouldKill)
                            {
                                if (app.HealthMonitoringEnabled)
                                {
                                    _notRespondingTracking.Remove(app.Index);
                                    _stableRunCheck.Remove(app.Index);
                                    _cpuTimeSamples.Remove(app.Index);
                                    _highCpuStreak.Remove(app.Index);

                                    SimpleLogger.Warn("UpdateApplicationStatuses @ Form1.cs",
                                        $"'{app.AppName}' is still not responding � force-killing for auto-restart");

                                    _forceKillReason[app.Index] = "UI Freeze / Not Responding (force-killed by health monitor)";
                                    _processManager.StopApplication(app);
                                }
                                else
                                {
                                    // Do NOT remove _notRespondingTracking here � the process is not being killed,
                                    // so it will still be not-responding on the next tick. Keeping the tracking
                                    // entry prevents re-entering the first-detection branch and re-logging.
                                    NotifyHealthIssue(app, item, "UI Freeze / Not Responding");
                                }
                            }
                        }
                        continue;
                    }
                    else
                    {
                        // App is responding normally � clear any not-responding tracking
                        _notRespondingTracking.Remove(app.Index);
                    }

                    // Detect error dialog windows (ghost windows that are "responding" but show
                    // unhandled exception, crash, or error dialogs). These windows pump messages
                    // so Process.Responding returns true, but the app is effectively stuck.
                    // Only applies to KeepOpen apps that are running and authorized.
                    if (isRunning && wasRunning && snapshot.HasErrorDialogWindow)
                    {
                        if (!_errorDialogTracking.ContainsKey(app.Index))
                        {
                            // First detection � wait one more poll cycle to confirm
                            _errorDialogTracking[app.Index] = DateTime.Now;

                            if (!_notifiedHealthIssues.Contains(app.Index))
                            {
                                SimpleLogger.Warn("UpdateApplicationStatuses @ Form1.cs",
                                    $"'{app.AppName}' appears to have an error dialog: \"{snapshot.ErrorDialogTitle}\" � confirming on next check");
                            }
                        }
                        else
                        {
                            // Second consecutive detection � force-kill
                            if (app.HealthMonitoringEnabled)
                            {
                                _errorDialogTracking.Remove(app.Index);
                                _stableRunCheck.Remove(app.Index);
                                _cpuTimeSamples.Remove(app.Index);
                                _highCpuStreak.Remove(app.Index);

                                SimpleLogger.Warn("UpdateApplicationStatuses @ Form1.cs",
                                    $"'{app.AppName}' confirmed error dialog: \"{snapshot.ErrorDialogTitle}\" � force-killing for auto-restart");

                                _forceKillReason[app.Index] = "Error dialog detected: \"" + (snapshot.ErrorDialogTitle ?? "unknown") + "\" (force-killed by health monitor)";
                                _processManager.StopApplication(app);
                            }
                            else
                            {
                                // Do NOT remove _errorDialogTracking here � the process is not being killed,
                                // so the error dialog will still be present on the next tick. Keeping the tracking
                                // entry prevents re-entering the first-detection branch and re-logging.
                                NotifyHealthIssue(app, item, "Unhandled exception / Error dialog");
                            }
                        }
                        continue;
                    }
                    else
                    {
                        _errorDialogTracking.Remove(app.Index);
                    }

                    // Detect window title changes for KeepOpen apps with DetectTitleChange enabled.
                    // When a WinForms app shows an unhandled exception dialog (ThreadExceptionDialog),
                    // the dialog's title is just the app's product name � no error keyword appears.
                    // By comparing the current window title to the established title, we can detect
                    // this class of dialog that would otherwise be invisible to pattern matching.
                    // Requires 2 consecutive detections to avoid false positives from apps that
                    // legitimately change their title (e.g. showing a document name).
                    if (isRunning && wasRunning && app.DetectTitleChange
                        && snapshot.MainWindowTitle != null)
                    {
                        string knownTitle;
                        if (!_knownWindowTitles.TryGetValue(app.Index, out knownTitle))
                        {
                            // First time seeing this app running � record its title as the baseline
                            _knownWindowTitles[app.Index] = snapshot.MainWindowTitle;
                        }
                        else if (!string.IsNullOrEmpty(knownTitle)
                            && !string.IsNullOrEmpty(snapshot.MainWindowTitle)
                            && knownTitle != snapshot.MainWindowTitle)
                        {
                            // Title changed � check if this is a confirmed change
                            if (!_titleChangeTracking.ContainsKey(app.Index))
                            {
                                _titleChangeTracking[app.Index] = DateTime.Now;

                                if (!_notifiedHealthIssues.Contains(app.Index))
                                {
                                    SimpleLogger.Warn("UpdateApplicationStatuses @ Form1.cs",
                                        $"'{app.AppName}' window title changed: \"{knownTitle}\" to \"{snapshot.MainWindowTitle}\" � confirming on next check");
                                }
                            }
                            else
                            {
                                // Second consecutive detection � treat as error dialog
                                string changedTitle = snapshot.MainWindowTitle;

                                if (app.HealthMonitoringEnabled)
                                {
                                    _titleChangeTracking.Remove(app.Index);
                                    _knownWindowTitles.Remove(app.Index);
                                    _stableRunCheck.Remove(app.Index);
                                    _cpuTimeSamples.Remove(app.Index);
                                    _highCpuStreak.Remove(app.Index);

                                    SimpleLogger.Warn("UpdateApplicationStatuses @ Form1.cs",
                                        $"'{app.AppName}' confirmed title change to \"{changedTitle}\" (was \"{knownTitle}\") � force-killing as suspected error dialog");

                                    _forceKillReason[app.Index] = "Window title change: \"" + (knownTitle ?? "") + "\" ? \"" + (changedTitle ?? "") + "\" (force-killed by health monitor)";
                                    _processManager.StopApplication(app);
                                }
                                else
                                {
                                    // Do NOT remove tracking here � the process is not being killed,
                                    // so the title change will still be present on the next tick.
                                    NotifyHealthIssue(app, item, "Window title change � possible error dialog");
                                }
                                continue;
                            }
                        }
                        else
                        {
                            // Title is the same � clear any pending title change tracking
                            _titleChangeTracking.Remove(app.Index);
                        }
                    }
                    else if (!isRunning)
                    {
                        _knownWindowTitles.Remove(app.Index);
                        _titleChangeTracking.Remove(app.Index);
                    }

                    // Detect memory limit breach for KeepOpen apps with a configured MemoryLimitMB.
                    // Uses WorkingSet64 from the batch snapshot (lightweight kernel query, no overhead).
                    if (isRunning && wasRunning && app.MemoryLimitMB > 0)
                    {
                        long limitBytes = (long)app.MemoryLimitMB * 1024L * 1024L;
                        if (snapshot.PeakWorkingSetBytes > limitBytes)
                        {
                            long usedMB = snapshot.PeakWorkingSetBytes / (1024L * 1024L);

                            _stableRunCheck.Remove(app.Index);
                            _cpuTimeSamples.Remove(app.Index);
                            _highCpuStreak.Remove(app.Index);

                            if (app.HealthMonitoringEnabled)
                            {
                                SimpleLogger.Warn("UpdateApplicationStatuses @ Form1.cs",
                                    $"'{app.AppName}' exceeded memory limit ({usedMB}MB / {app.MemoryLimitMB}MB) � force-killing for auto-restart");

                                _forceKillReason[app.Index] = "Memory limit exceeded (" + usedMB + "MB / " + app.MemoryLimitMB + "MB) (force-killed by health monitor)";
                                _processManager.StopApplication(app);
                            }
                            else
                            {
                                NotifyHealthIssue(app, item, "Memory limit exceeded / Possible memory leak");
                            }
                            // The next tick will detect the app as stopped and trigger crash/restart logic
                            continue;
                        }
                    }

                    // Detect CPU-hung processes for KeepOpen apps.
                    // Compares TotalProcessorTime across poll ticks to compute CPU utilization.
                    // If CPU usage exceeds HighCpuThreshold for HighCpuStreakThreshold consecutive
                    // ticks, the process is considered hung (infinite loop, deadlock spin, etc.).
                    // This uses only Process.TotalProcessorTime which is a lightweight kernel query.
                    if (isRunning && wasRunning && snapshot.TotalCpuTime.Ticks > 0)
                    {
                        KeyValuePair<DateTime, TimeSpan> lastSample;
                        if (_cpuTimeSamples.TryGetValue(app.Index, out lastSample))
                        {
                            double elapsedSeconds = (DateTime.Now - lastSample.Key).TotalSeconds;
                            if (elapsedSeconds > 0.5) // Avoid division by near-zero
                            {
                                double cpuSeconds = (snapshot.TotalCpuTime - lastSample.Value).TotalSeconds;
                                double cpuUsage = cpuSeconds / (elapsedSeconds * Environment.ProcessorCount);

                                if (cpuUsage >= HighCpuThreshold)
                                {
                                    int streak;
                                    _highCpuStreak.TryGetValue(app.Index, out streak);
                                    streak++;
                                    _highCpuStreak[app.Index] = streak;

                                    if (streak >= HighCpuStreakThreshold)
                                    {
                                        if (app.HealthMonitoringEnabled)
                                        {
                                            _highCpuStreak.Remove(app.Index);
                                            _cpuTimeSamples.Remove(app.Index);
                                            _stableRunCheck.Remove(app.Index);

                                            SimpleLogger.Warn("UpdateApplicationStatuses @ Form1.cs",
                                                $"'{app.AppName}' has been at {(cpuUsage * 100):F0}% CPU for {streak} consecutive checks � force-killing as CPU-hung");

                                            _forceKillReason[app.Index] = "CPU-hung (" + ((int)(cpuUsage * 100)) + "% for " + streak + " consecutive checks) (force-killed by health monitor)";
                                            _processManager.StopApplication(app);
                                        }
                                        else
                                        {
                                            // Reset streak so it can re-detect if the issue persists,
                                            // but NotifyHealthIssue ensures only one notification per issue.
                                            _highCpuStreak.Remove(app.Index);
                                            NotifyHealthIssue(app, item, "CPU spin loop / High CPU usage");
                                        }
                                        continue;
                                    }
                                    else
                                    {
                                        // Only log the building streak if not already notified
                                        if (!_notifiedHealthIssues.Contains(app.Index))
                                        {
                                            SimpleLogger.Debug("UpdateApplicationStatuses @ Form1.cs",
                                                $"'{app.AppName}' high CPU: {(cpuUsage * 100):F0}% (streak {streak}/{HighCpuStreakThreshold})");
                                        }
                                    }
                                }
                                else
                                {
                                    // CPU back to normal � reset streak
                                    _highCpuStreak.Remove(app.Index);
                                }
                            }
                        }

                        // Update the sample for next tick comparison
                        _cpuTimeSamples[app.Index] = new KeyValuePair<DateTime, TimeSpan>(DateTime.Now, snapshot.TotalCpuTime);
                    }
                    else if (!isRunning)
                    {
                        // Clean up CPU tracking for stopped apps
                        _cpuTimeSamples.Remove(app.Index);
                        _highCpuStreak.Remove(app.Index);
                    }

                    // Detect unauthorized external launch (or cross-group detection)
                    if (isRunning && !_authorizedApps.Contains(app.Index))
                    {
                        // Before treating as unauthorized, check if another entry for the same
                        // executable is authorized (same app added to multiple groups).
                        // In that case, the process was legitimately started from another group.
                        // Uses FindAuthorizedSibling to avoid allocating a List on every tick.
                        var authorizedSibling = _storageService.FindAuthorizedSibling(app.Directory, app.Index, _authorizedApps);

                        if (authorizedSibling != null)
                        {
                            // The process is running because it was started from another group entry.
                            // Don't kill it � just update the display to reflect that it's running elsewhere.
                            // Ensure IsRunning stays false for this entry so group indicators stay correct.
                            if (app.IsRunning)
                            {
                                _storageService.UpdateApplicationFields(app.Index, isRunning: false);
                            }
                            if (item != null)
                            {
                                item.SubItems[3].Text = "Running (Other Group)";
                                item.ForeColor = Color.DarkCyan;
                            }
                            continue;
                        }

                        // Only treat as unauthorized if this is a new detection (wasn't running before)
                        if (!wasRunning)
                        {
                            HandleUnauthorizedLaunch(app, item);
                            continue;
                        }
                    }

                    // App was stopped externally (outside IMEE) � possible crash
                    // This also handles background zombie processes: when a process crashes,
                    // it may leave behind a lingering background process (no window). We handle
                    // zombie cleanup here as part of crash detection so the exit code and crash
                    // reason are properly classified instead of being masked as a generic "zombie".
                    if (!isRunning && wasRunning)
                    {
                        _authorizedApps.Remove(app.Index);
                        _notifiedUnauthorized.Remove(app.Index);
                        _notifiedHealthIssues.Remove(app.Index);
                        _stableRunCheck.Remove(app.Index);

                        DateTime stopTime = DateTime.Now;

                        // Retrieve exit code from the tracked Process handle before it's cleaned up
                        int? exitCode = _processManager.GetTrackedExitCode(app.Index);

                        // Check if this exit was triggered by a health monitoring force-kill.
                        // If so, the exit code reflects the forced termination (e.g., exit code 1
                        // from TerminateProcess) � not the original issue. Use the pre-recorded
                        // reason instead of classifying the forced termination's exit code.
                        string preRecordedReason;
                        bool wasForceKilled = _forceKillReason.TryGetValue(app.Index, out preRecordedReason);
                        _forceKillReason.Remove(app.Index);

                        if (exitCode.HasValue)
                        {
                            // Diagnostic: log tracking details to help identify cases where
                            // the recorded exit code (e.g. 0xFFFFFFFF) may come from a
                            // launcher or stale handle instead of the real app process.
                            try
                            {
                                int trackedPid = _processManager.GetTrackedPid(app.Index);
                                bool hasHandle = _processManager.HasTrackedHandle(app.Index);
                                SimpleLogger.Debug("UpdateApplicationStatuses @ Form1.cs",
                                    $"ExitCode diagnostic: TrackedPid={trackedPid}, HasTrackedHandle={hasHandle}, FirstWindowedPid={snapshot.FirstWindowedPid}, LauncherConfigured={!string.IsNullOrEmpty(app.LauncherPath)}");
                            }
                            catch (Exception) { }

                            if (wasForceKilled)
                            {
                                SimpleLogger.Info("UpdateApplicationStatuses @ Form1.cs",
                                    $"'{app.AppName}' exited with code {FormatExitCode(exitCode.Value)} (health monitor force-kill � original reason: {preRecordedReason})");
                            }
                            else
                            {
                                string crashReason = ClassifyExitCode(exitCode.Value);
                                SimpleLogger.Info("UpdateApplicationStatuses @ Form1.cs",
                                    $"'{app.AppName}' exited with code {FormatExitCode(exitCode.Value)} ({crashReason})");
                            }
                        }

                        // Clean up any lingering background/zombie processes left behind by the crash.
                        // This must happen AFTER exit code retrieval but BEFORE restart scheduling.
                        bool hadZombies = false;
                        if (snapshot.HasBackgroundProcess)
                        {
                            hadZombies = true;
                            int killed = _processManager.KillBackgroundProcesses(app);
                            SimpleLogger.Warn("UpdateApplicationStatuses @ Form1.cs",
                                $"'{app.AppName}' had {killed} lingering background process(es) after exit � cleaned up");
                        }

                        // If KeepOpen is enabled, treat this as a crash and schedule restart
                        if (app.KeepOpen)
                        {
                            // If the process exited normally (exit code 0) and it wasn't force-killed
                            // by the health monitor, do NOT treat this as a crash. Some applications
                            // legitimately exit with code 0 shortly after start (e.g. helpers/one-shot
                            // launchers) and should not be auto-restarted just because KeepOpen is set.
                            if (!wasForceKilled && exitCode.HasValue && exitCode.Value == 0)
                            {
                                // Record stopped state but do not increment crash/retry counters
                                _storageService.UpdateApplicationFields(app.Index, isRunning: false,
                                    lastStop: stopTime, lastExitCode: exitCode);

                                if (item != null)
                                {
                                    item.SubItems[3].Text = "Stopped";
                                    item.SubItems[8].Text = stopTime.ToString("yyyy-MM-dd HH:mm:ss");
                                    item.SubItems[9].Text = FormatExitCodeDisplay(exitCode);
                                    item.ForeColor = Color.Black;

                                    var tagApp = item.Tag as ManagedApplication;
                                    if (tagApp != null)
                                    {
                                        tagApp.LastStop = stopTime;
                                        tagApp.IsRunning = false;
                                        if (exitCode.HasValue) tagApp.LastExitCode = exitCode.Value;
                                    }
                                }

                                SimpleLogger.Info("UpdateApplicationStatuses @ Form1.cs",
                                    $"'{app.AppName}' exited normally (KeepOpen=Yes) � not scheduling restart");

                                // Do not schedule a restart for a normal exit
                                continue;
                            }

                            int newCrashCount = app.CrashCount + 1;
                            int newRetryCount = app.RetryCount + 1;

                            // Build a descriptive crash reason for logging.
                            // If the process was force-killed by health monitoring, use the
                            // pre-recorded reason (which describes the actual issue) instead
                            // of classifying the forced termination's exit code.
                            string detailedReason;
                            if (wasForceKilled)
                            {
                                detailedReason = preRecordedReason;
                                if (hadZombies)
                                {
                                    detailedReason = detailedReason + " + zombie process(es) cleaned up";
                                }
                            }
                            else
                            {
                                detailedReason = BuildCrashDescription(exitCode, hadZombies);
                            }

                            // Check if max retries exhausted
                            if (newRetryCount >= app.MaxRetries)
                            {
                                SimpleLogger.Error("UpdateApplicationStatuses @ Form1.cs",
                                    $"'{app.AppName}' exceeded max retries ({newRetryCount}/{app.MaxRetries}). " +
                                    $"Crash reason: {detailedReason}. Giving up.");

                                _failedApps.Add(app.Index);

                                _storageService.UpdateApplicationFields(app.Index, isRunning: false,
                                    lastStop: stopTime, crashCount: newCrashCount, retryCount: newRetryCount,
                                    lastExitCode: exitCode);

                                if (item != null)
                                {
                                    item.SubItems[3].Text = "Failed";
                                    item.SubItems[5].Text = newCrashCount.ToString();
                                    item.SubItems[6].Text = newRetryCount.ToString();
                                    item.SubItems[8].Text = stopTime.ToString("yyyy-MM-dd HH:mm:ss");
                                    item.SubItems[9].Text = FormatExitCodeDisplay(exitCode);
                                    item.ForeColor = Color.Red;

                                    var tagApp = item.Tag as ManagedApplication;
                                    if (tagApp != null)
                                    {
                                        tagApp.CrashCount = newCrashCount;
                                        tagApp.RetryCount = newRetryCount;
                                        tagApp.LastStop = stopTime;
                                        tagApp.IsRunning = false;
                                        if (exitCode.HasValue) tagApp.LastExitCode = exitCode.Value;
                                    }
                                }

                                NotifyMaxRetriesExhausted(app, detailedReason);
                            }
                            else
                            {
                                // Only auto-restart if health monitoring is enabled for this app
                                if (app.HealthMonitoringEnabled && app.KeepOpen)
                                {
                                    int delay = Math.Max(app.StartDelaySeconds, 1);
                                    _pendingRestart[app.Index] = DateTime.Now.AddSeconds(delay);

                                    // Start the 1-second countdown timer if not already running
                                    if (!_countdownTimer.Enabled)
                                    {
                                        _countdownTimer.Start();
                                    }

                                    SimpleLogger.Warn("UpdateApplicationStatuses @ Form1.cs",
                                        $"'{app.AppName}' crashed (KeepOpen=Yes). Crash reason: {detailedReason}. " +
                                        $"Crash #{newCrashCount}, retry {newRetryCount}/{app.MaxRetries}, scheduling restart in {delay}s");

                                    _storageService.UpdateApplicationFields(app.Index, isRunning: false,
                                        lastStop: stopTime, crashCount: newCrashCount, retryCount: newRetryCount,
                                        lastExitCode: exitCode);

                                    if (item != null)
                                    {
                                        item.SubItems[3].Text = $"Restarting ({delay}s)";
                                        item.SubItems[5].Text = newCrashCount.ToString();
                                        item.SubItems[6].Text = newRetryCount.ToString();
                                        item.SubItems[8].Text = stopTime.ToString("yyyy-MM-dd HH:mm:ss");
                                        item.SubItems[9].Text = FormatExitCodeDisplay(exitCode);
                                        item.ForeColor = Color.DarkOrange;

                                        var tagApp = item.Tag as ManagedApplication;
                                        if (tagApp != null)
                                        {
                                            tagApp.CrashCount = newCrashCount;
                                            tagApp.RetryCount = newRetryCount;
                                            tagApp.LastStop = stopTime;
                                            tagApp.IsRunning = false;
                                            if (exitCode.HasValue) tagApp.LastExitCode = exitCode.Value;
                                        }
                                    }
                                }
                                else
                                {
                                    // Health monitoring disabled: just notify and log
                                    SimpleLogger.Warn("UpdateApplicationStatuses @ Form1.cs",
                                        $"'{app.AppName}' crashed but health monitoring disabled. " +
                                        $"Crash reason: {detailedReason}. Not scheduling auto-restart.");
                                    _storageService.UpdateApplicationFields(app.Index, isRunning: false,
                                        lastStop: stopTime, crashCount: newCrashCount, retryCount: newRetryCount,
                                        lastExitCode: exitCode);
                                    if (item != null)
                                    {
                                        item.SubItems[3].Text = "Stopped (Issue Detected)";
                                        item.SubItems[5].Text = newCrashCount.ToString();
                                        item.SubItems[6].Text = newRetryCount.ToString();
                                        item.SubItems[8].Text = stopTime.ToString("yyyy-MM-dd HH:mm:ss");
                                        item.SubItems[9].Text = FormatExitCodeDisplay(exitCode);
                                        item.ForeColor = Color.DarkOrange;
                                    }
                                    NotifyHealthIssue(app, item, detailedReason);
                                }
                            }
                        }
                        else
                        {
                            // Non-KeepOpen app stopped externally � check if it was an abnormal exit
                            // and notify the user if so (crash detection for non-KeepOpen apps)
                            string detailedReason;
                            if (wasForceKilled)
                            {
                                detailedReason = preRecordedReason;
                                if (hadZombies)
                                {
                                    detailedReason = detailedReason + " + zombie process(es) cleaned up";
                                }
                            }
                            else
                            {
                                detailedReason = BuildCrashDescription(exitCode, hadZombies);
                            }

                            // Treat as abnormal if:
                            // 1. Exit code is non-zero (definite crash), OR
                            // 2. Exit code is unavailable (null) � the app unexpectedly disappeared
                            //    (e.g., OS killed it for Out of Memory before IMEE could read the exit code)
                            // Only exit code 0 (confirmed normal exit) is treated as a clean stop.
                            bool isAbnormalExit = !exitCode.HasValue || exitCode.Value != 0;

                            int newCrashCount = app.CrashCount + (isAbnormalExit ? 1 : 0);

                            _storageService.UpdateApplicationFields(app.Index, isRunning: false, lastStop: stopTime,
                                lastExitCode: exitCode, crashCount: isAbnormalExit ? (int?)newCrashCount : null);

                            if (item != null)
                            {
                                if (isAbnormalExit)
                                {
                                    item.SubItems[3].Text = "Stopped (Crashed)";
                                    item.SubItems[5].Text = newCrashCount.ToString();
                                    item.ForeColor = Color.DarkOrange;
                                }
                                else
                                {
                                    item.SubItems[3].Text = "Stopped";
                                    item.ForeColor = Color.Black;
                                }
                                item.SubItems[8].Text = stopTime.ToString("yyyy-MM-dd HH:mm:ss");
                                item.SubItems[9].Text = FormatExitCodeDisplay(exitCode);

                                var tagApp = item.Tag as ManagedApplication;
                                if (tagApp != null)
                                {
                                    tagApp.LastStop = stopTime;
                                    tagApp.IsRunning = false;
                                    if (isAbnormalExit) tagApp.CrashCount = newCrashCount;
                                    if (exitCode.HasValue) tagApp.LastExitCode = exitCode.Value;
                                }
                            }

                            if (isAbnormalExit)
                            {
                                SimpleLogger.Warn("UpdateApplicationStatuses @ Form1.cs",
                                    $"'{app.AppName}' crashed (KeepOpen=No). Crash reason: {detailedReason}");
                                NotifyHealthIssue(app, item, detailedReason);
                            }
                            else
                            {
                                SimpleLogger.Info("UpdateApplicationStatuses @ Form1.cs",
                                    $"'{app.AppName}' was stopped externally");
                            }
                        }
                    }
                    // Catch-all: ensure running, authorized apps display "Running" (green).
                    // This handles edge cases where the status text was left in a transitional
                    // state (e.g., "Starting..." after grace period expiry, or "Warning" after
                    // a health issue clears) and no earlier branch updated it.
                    if (isRunning && item != null)
                    {
                        string currentStatus = item.SubItems[3].Text;
                        if (currentStatus != "Running")
                        {
                            item.SubItems[3].Text = "Running";
                            item.ForeColor = Color.Green;
                        }
                    }
                }

                // Update group indicators on every tick so they stay in sync with app statuses
                UpdateGroupIndicators(allApps);

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
                    NotifyMaxRetriesExhausted(app, "Max retries exceeded");
                    return;
                }

                // Validate the primary exe (needed for process detection)
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
                    NotifyMaxRetriesExhausted(app, "Application file not found: " + (app.Directory ?? "(empty)"));
                    return;
                }

                // Validate launcher if configured
                if (!string.IsNullOrEmpty(app.LauncherPath) && !File.Exists(app.LauncherPath))
                {
                    SimpleLogger.Error("AttemptAutoRestart @ Form1.cs",
                        $"Cannot auto-restart '{app.AppName}': launcher not found at {app.LauncherPath}");
                    _failedApps.Add(app.Index);
                    if (item != null)
                    {
                        item.SubItems[3].Text = "Failed";
                        item.ForeColor = Color.Red;
                    }
                    NotifyMaxRetriesExhausted(app, "Launcher file not found: " + app.LauncherPath);
                    return;
                }

                if (_processManager.StartApplication(app))
                {
                    _authorizedApps.Add(app.Index);
                    _notifiedUnauthorized.Remove(app.Index);
                    _notifiedHealthIssues.Remove(app.Index);
                    _pendingStop.Remove(app.Index);
                    _failedApps.Remove(app.Index);
                    _forceKillReason.Remove(app.Index);

                    // Grant a grace period so the watchdog doesn't treat the app as crashed
                    // before its main window has had time to appear
                    _startGracePeriod[app.Index] = DateTime.Now.AddSeconds(_settingsService.StartGracePeriodSeconds);

                    // Schedule a stable run check � retry count resets after this period
                    int stablePeriod = Math.Max(app.StableRunPeriodSeconds, 5);
                    _stableRunCheck[app.Index] = DateTime.Now.AddSeconds(stablePeriod);

                    DateTime now = DateTime.Now;
                    // Do NOT reset retryCount here � it will be reset only after the app
                    // runs stably for the configured StableRunPeriodSeconds.
                    // Retry count resets only on: (1) manual start by user, or (2) stable run period.
                    _storageService.UpdateApplicationFields(app.Index, isRunning: true, lastStart: now);

                    if (item != null)
                    {
                        item.SubItems[3].Text = "Starting...";
                        item.SubItems[7].Text = app.GetLastStartDisplay();
                        item.ForeColor = Color.DarkOrange;

                        var tagApp = item.Tag as ManagedApplication;
                        if (tagApp != null)
                        {
                            tagApp.LastStart = app.LastStart;
                            tagApp.IsRunning = true;
                        }
                    }

                    // Refresh group indicator immediately
                    UpdateGroupIndicators(_storageService.GetAllApplicationsReadOnly());
                    SimpleLogger.Info("AttemptAutoRestart @ Form1.cs",
                        $"Auto-restarted '{app.AppName}' successfully (Retry {app.RetryCount}/{app.MaxRetries}), " +
                        $"retry count will reset after {stablePeriod}s of stable running");
                    return;
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

                    NotifyMaxRetriesExhausted(app, "Auto-restart failed (process could not be started)");
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

                NotifyMaxRetriesExhausted(app, "Auto-restart error: " + ex.Message);
            }
        }

        /// <summary>
        /// Handles when a managed application is launched outside of IMEE.
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
                                    $"'{appName}' was launched outside of IMEE and has been terminated.\n\n" +
                                    "Please use IMEE to start managed applications.");

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

        /// <summary>
        /// Notifies user and logs a detected health issue when HealthMonitoringEnabled is false.
        /// Ensures notifications and log messages are not repeatedly shown/written for the same app
        /// until the issue is cleared by a user action (start, stop, delete).
        /// </summary>
        private void NotifyHealthIssue(ManagedApplication app, ListViewItem item, string issue)
        {
            //if (item != null)
            //{
            //    item.SubItems[3].Text = "Warning";
            //    item.ForeColor = Color.DarkOrange;
            //}

            // Only log and notify once per health issue � prevents log flooding
            if (_notifiedHealthIssues.Contains(app.Index))
                return;

            _notifiedHealthIssues.Add(app.Index);

            string exitCodeInfo = app.LastExitCode.HasValue
                ? $"Last Exit Code: {FormatExitCode(app.LastExitCode.Value)}" + (app.LastExitCode.Value != 0 ? " (abnormal)" : "")
                : "Last Exit Code: N/A";

            SimpleLogger.Warn("HealthMonitor @ Form1.cs",
                $"Health issue detected for '{app.AppName}': {issue}. {exitCodeInfo}. " +
                "Automatic corrective action is DISABLED for this application (HealthMonitoringEnabled=false). " +
                "IMEE will not kill or restart this process. User acknowledgement required.");

            try
            {
                if (!_isClosing && IsHandleCreated)
                {
                    string appName = app.AppName;
                    string issueCopy = issue;
                    string exitCodeCopy = exitCodeInfo;
                    this.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (_isClosing) return;

                            MessageBoxHelper.ShowWarning(this,
                                $"'{appName}' may be experiencing: {issueCopy}.\n\n" +
                                $"{exitCodeCopy}\n\n" +
                                "IMEE is configured to NOT take automatic corrective action for this application.");

                            SimpleLogger.Info("HealthMonitor @ Form1.cs",
                                $"User acknowledged health issue for '{appName}': {issueCopy}. {exitCodeCopy}. " +
                                "No further notifications will be shown for this issue until the user takes action (start, stop, or delete).");
                        }
                        catch (Exception ex)
                        {
                            SimpleLogger.Error("NotifyHealthIssue @ Form1.cs", $"Error showing health notification: {ex.Message}");
                        }
                    }));
                }
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        /// <summary>
        /// Notifies user and logs when an application has exhausted its maximum number of restart attempts.
        /// </summary>
        private void NotifyMaxRetriesExhausted(ManagedApplication app, string reason)
        {
            SimpleLogger.Error("NotifyMaxRetriesExhausted @ Form1.cs",
                $"'{app.AppName}' has exhausted max retries. Reason: {reason}");
            try
            {
                if (!_isClosing && IsHandleCreated)
                {
                    string appName = app.AppName;
                    string reasonCopy = reason;
                    this.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (_isClosing) return;
                            MessageBoxHelper.ShowError(this,
                                $"'{appName}' has exceeded the maximum number of restart attempts and will not be restarted again.\n\n" +
                                $"Reason: {reasonCopy}\n\n" +
                                "Please investigate the issue and manually restart the application when ready.");
                            SimpleLogger.Info("NotifyMaxRetriesExhausted @ Form1.cs",
                                $"User acknowledged max retries exhausted for '{appName}'. Reason: {reasonCopy}. " +
                                "No further automatic restart attempts will be made for this application.");
                        }
                        catch (Exception ex)
                        {
                            SimpleLogger.Error("NotifyMaxRetriesExhausted @ Form1.cs", $"Error showing max retries exhausted notification: {ex.Message}");
                        }
                    }));
                }
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        /// <summary>
        /// Formats an exit code for display in the ListView and dialogs.
        /// Non-zero codes are shown in hex (e.g., "0xC0000005") for easier lookup.
        /// Zero is shown as "0" since it's always a normal exit.
        /// </summary>
        private static string FormatExitCode(int exitCode)
        {
            if (exitCode == 0)
                return "0";
            uint code = unchecked((uint)exitCode);
            return "0x" + code.ToString("X8");
        }

        /// <summary>
        /// Formats a nullable exit code for display: hex for non-zero, "0" for zero, "�" for null.
        /// </summary>
        private static string FormatExitCodeDisplay(int? exitCode)
        {
            if (!exitCode.HasValue)
                return "�";
            return FormatExitCode(exitCode.Value);
        }

        /// <summary>
        /// Classifies a process exit code into a human-readable crash reason.
        /// Common Windows and .NET exit codes are mapped to descriptive names.
        /// </summary>
        private static string ClassifyExitCode(int exitCode)
        {
            // Use unsigned comparison for NTSTATUS/Win32 codes
            uint code = unchecked((uint)exitCode);

            switch (code)
            {
                // Normal exits
                case 0x00000000:
                    return "Normal exit";
                case 0x00000001:
                    return "General error";
                case 0x00000002:
                    return "File not found";
                case 0x00000003:
                    return "Path not found";

                // FIX: 0xC000000B is STATUS_INVALID_CID (invalid process/thread client ID),
                //      NOT a timeout. Wait-timeout is STATUS_TIMEOUT = 0x00000102.
                case 0x00000102:
                    return $"Wait operation timed out (STATUS_TIMEOUT) � common in frozen UI threads";
                case 0xC000000B:
                    return $"Invalid client ID � process or thread ID not found in system (STATUS_INVALID_CID)";
                case 0x800705B4:
                    return $"Generic timeout";
                case 0x000000EF:
                    return $"Critical system process stopped";
                // FIX: 0x0000007E is BSOD SYSTEM_THREAD_EXCEPTION_NOT_HANDLED,
                //      an unhandled exception in a system thread � not simply "frozen"
                case 0x0000007E:
                    return $"BSOD: unhandled exception in a system thread (SYSTEM_THREAD_EXCEPTION_NOT_HANDLED)";
                // FIX: 0x00000139 is BSOD KERNEL_SECURITY_CHECK_FAILURE �
                //      a stack cookie, CFG, or other security integrity check failed
                case 0x00000139:
                    return $"BSOD: kernel security check failure (stack cookie, CFG, or integrity violation)";
                case 0x00000101:
                    return $"CPU core stopped responding � classic full system freeze";
                case 0x00000133:
                    return $"Deferred Procedure Call took too long � often causes UI hangs";

                // NTSTATUS informational / warnings (0x8xxxxxxx)
                case 0x80000001: // -2147483647
                    return $"Guard page violation";
                case 0x80000002: // -2147483646
                    return $"Datatype misalignment";
                case 0x80000003: // -2147483645
                    return $"Breakpoint hit";
                case 0x80000004: // -2147483644
                    return $"Single step (debugger)";

                // NTSTATUS error codes (0xCxxxxxxx)
                case 0xC0000005: // -1073741819
                    return $"Access violation";
                case 0xC0000006: // -1073741818
                    return $"In-page error (page fault)";
                case 0xC000000D: // -1073741811
                    return $"Invalid parameter";
                case 0xC0000017: // -1073741801
                    return $"Out of memory (no memory)";
                case 0xC000001D: // -1073741795
                    return $"Illegal instruction";
                case 0xC0000025: // -1073741787
                    return $"Non-continuable exception";
                case 0xC000007B: // -1073741701
                    return $"Invalid image format (wrong architecture or corrupt executable)";
                case 0xC000007F: // -1073741697
                    return $"Disk full";
                case 0xC000008C: // -1073741684
                    return $"Array bounds exceeded";
                case 0xC000008D: // -1073741683
                    return $"Floating point denormal operand";
                case 0xC000008E: // -1073741682
                    return $"Floating point divide by zero";
                case 0xC000008F: // -1073741681
                    return $"Floating point inexact result";
                case 0xC0000090: // -1073741680
                    return $"Floating point invalid operation";
                case 0xC0000091: // -1073741679
                    return $"Floating point overflow";
                case 0xC0000092: // -1073741678
                    return $"Floating point stack check";
                case 0xC0000093: // -1073741677
                    return $"Floating point underflow";
                case 0xC0000094: // -1073741676
                    return $"Integer divide by zero";
                case 0xC0000095: // -1073741675
                    return $"Integer overflow";
                case 0xC0000096: // -1073741674
                    return $"Privileged instruction";
                case 0xC000009A: // -1073741670
                    return $"Insufficient system resources";
                case 0xC0000120: // -1073741536
                    return $"Operation cancelled (thread abort / CancellationToken)";
                case 0xC00000FD: // -1073741571
                    return $"Stack overflow";
                case 0xC0000102: // -1073741054
                    return $"File corrupt / disk error";
                case 0xC0000135: // -1073741515
                    return $"DLL not found";
                case 0xC0000138: // -1073741512
                    return $"DLL ordinal not found";
                case 0xC0000139: // -1073741511
                    return $"DLL entry point not found";
                case 0xC000013A: // -1073741510
                    return $"Process terminated by Ctrl+C";
                case 0xC0000142: // -1073741502
                    return $"DLL initialization failed";
                case 0xC0000185: // -1073741435
                    return $"I/O device error";
                case 0xC0000194: // -1073741420
                    return $"Possible deadlock (wait timeout)";
                case 0xC0000374: // -1073740940
                    return $"Heap corruption";
                case 0xC0000409: // -1073740791
                    return $"Stack buffer overrun (/GS security cookie)";
                case 0xC0000417: // -1073740777
                    return $"Invalid C runtime parameter";
                case 0xC000041B: // -1073740773
                    return $"Fatal user callback exception";
                case 0xC000041D: // -1073740771
                    return $"Fatal app exit / terminate() called";
                case 0xC0000420: // -1073740768
                    return $"Assertion failure";
                // REMOVED: 0xC0000602 was incorrectly labelled "Unknown software exception (WER)".
                //          On Windows 10+, 0xC0000602 = STATUS_CLOUD_FILE_NOT_SUPPORTED (OneDrive placeholder).
                //          WER unknown software exceptions belong in the 0xE0xxxxxx range (e.g. 0xE06D7363).

                // --- Elevation / UAC / privilege failures ---
                case 0x800702E4: // ERROR_ELEVATION_REQUIRED
                    return $"Elevation required � application must be run as administrator";
                case 0xC0000061: // STATUS_PRIVILEGE_NOT_HELD
                    return $"Required privilege not held (missing admin or SE_* privilege)";
                case 0xC000007C: // STATUS_NO_TOKEN
                    return $"No impersonation token (access token missing or invalid)";
                case 0xC0000022: // STATUS_ACCESS_DENIED
                    return $"Access denied (may require elevation or admin rights)";
                case 0x80070005: // E_ACCESSDENIED / ERROR_ACCESS_DENIED
                    return $"Access denied (COM/Win32 � may require administrator rights)";
                case 0x80070522: // ERROR_PRIVILEGE_NOT_HELD
                    return $"A required privilege is not held by the client (missing admin right)";
                // FIX: 0x80070542 = Win32 error 1346 = ERROR_BAD_IMPERSONATION_LEVEL,
                //      not ERROR_ONLY_IF_CONNECTED (which is 1251 = 0x800704E3)
                case 0x80070542: // ERROR_BAD_IMPERSONATION_LEVEL
                    return $"Bad impersonation level � required impersonation level not provided or invalid";

                // --- Network drive / UNC path failures ---
                case 0x80070035: // ERROR_BAD_NETPATH
                    return $"Network path not found (disconnected or unavailable network drive)";
                case 0x80070037: // ERROR_DEV_NOT_EXIST
                    return $"Network device no longer exists (drive was disconnected)";
                case 0x80070040: // ERROR_NETNAME_DELETED
                    return $"Network name deleted (share removed or connection dropped)";
                case 0x80070041: // ERROR_NETWORK_ACCESS_DENIED
                    return $"Network access denied (credentials invalid or share permissions)";
                case 0x80070043: // ERROR_BAD_NET_NAME
                    return $"Network name cannot be found (bad UNC path or share name)";
                case 0x80070044: // ERROR_TOO_MANY_NAMES
                    return $"Too many network names registered";
                case 0x80070045: // ERROR_TOO_MANY_SESS
                    return $"Too many remote sessions � server refused connection";
                case 0x80070046: // ERROR_SHARING_PAUSED
                    return $"Network sharing is paused on the remote server";
                case 0x8007003B: // ERROR_UNEXP_NET_ERR
                    return $"Unexpected network error (connection lost mid-operation)";
                case 0x80070571: // ERROR_DISK_CORRUPT
                    return $"Disk structure is corrupt and unreadable (possibly mapped drive)";
                case 0x800700DF: // ERROR_NO_NET_OR_BAD_NET
                    return $"No network available or network configuration error";

                // --- General Win32 / application startup failures ---
                case 0x80070006: // E_HANDLE
                    return $"Invalid handle (closed, revoked, or never opened)";
                case 0x8007000B: // ERROR_BAD_FORMAT
                    return $"Bad executable format (corrupt or incompatible binary)";
                case 0x8007000E: // E_OUTOFMEMORY
                    return $"Out of memory (Win32 heap allocation failed)";
                case 0x80070057: // E_INVALIDARG
                    return $"Invalid argument passed to Win32 API";
                case 0x8007007B: // ERROR_INVALID_NAME
                    return $"Invalid file or path name (bad characters or malformed path)";
                case 0x8007007E: // ERROR_MOD_NOT_FOUND
                    return $"Module (DLL) not found � dependency missing";
                case 0x8007007F: // ERROR_PROC_NOT_FOUND
                    return $"Procedure not found in DLL (version mismatch or wrong DLL)";
                case 0x800700C1: // ERROR_BAD_EXE_FORMAT
                    return $"Bad EXE format (wrong bitness, e.g. 16-bit app on 64-bit OS)";
                case 0x800700C2: // ERROR_ITERATED_DATA_EXCEEDS_64k
                    return $"Iterated data exceeds 64KB (corrupt or ancient 16-bit binary)";
                // FIX: 0x800700BF = Win32 error 191 = ERROR_INVALID_EXE_SIGNATURE (not 0x800701F4)
                case 0x800700BF: // ERROR_INVALID_EXE_SIGNATURE
                    return $"Invalid executable signature (not a valid PE image)";
                // FIX: 0x800701F4 = Win32 error 500 = ERROR_INVALID_USER_BUFFER,
                //      not ERROR_INVALID_EXE_SIGNATURE (corrected above)
                case 0x800701F4: // ERROR_INVALID_USER_BUFFER
                    return $"Invalid user buffer � supplied buffer is not valid for the requested operation";
                case 0x80070216: // ERROR_ARITHMETIC_OVERFLOW
                    return $"Arithmetic overflow in Win32 API call";
                // FIX: 0x800700E7 = Win32 error 231 = ERROR_PIPE_BUSY (not 0x80070218)
                case 0x800700E7: // ERROR_PIPE_BUSY
                    return $"Named pipe busy � server not accepting connections yet";
                // FIX: 0x80070218 = Win32 error 536 = ERROR_PIPE_CONNECTED,
                //      not ERROR_PIPE_BUSY (corrected above)
                case 0x80070218: // ERROR_PIPE_CONNECTED
                    return $"Named pipe connected � there is a process on the other end of the pipe";
                case 0x80070490: // ERROR_NOT_FOUND
                    return $"Element not found (registry key, file, or resource missing)";
                case 0x800704C7: // ERROR_CANCELLED
                    return $"Operation cancelled by user (UAC prompt dismissed)";
                // FIX: 0x800704DC = Win32 error 1244 = ERROR_NOT_AUTHENTICATED (was incorrectly on 0xDD)
                case 0x800704DC: // ERROR_NOT_AUTHENTICATED
                    return $"Not authenticated � credentials required";
                // FIX: 0x800704DD = Win32 error 1245 = ERROR_NOT_LOGGED_ON (was incorrectly on 0xDD)
                case 0x800704DD: // ERROR_NOT_LOGGED_ON
                    return $"User not logged on (service or session issue)";
                // REMOVED: 0x800704DE = Win32 error 1246 = ERROR_CONTINUE (instructs handler to continue),
                //          not a logon or authentication error. Not meaningful as an exit code.
                case 0x80040154: // REGDB_E_CLASSNOTREG
                    return $"COM class not registered (missing COM component or 32/64-bit mismatch)";
                case 0x80004003: // E_POINTER
                    return $"Null pointer (COM/interop E_POINTER)";
                case 0x80004005: // E_FAIL
                    return $"Unspecified COM/interop failure (E_FAIL)";

                // --- Network / socket / WinSock errors ---
                case 0x80072742: // WSAENETDOWN
                    return $"Network is down (local network adapter or stack failure)";
                case 0x80072743: // WSAENETUNREACH
                    return $"Network unreachable (no route to host or gateway down)";
                case 0x80072744: // WSAENETRESET
                    return $"Network connection reset (keep-alive failure)";
                case 0x80072745: // WSAECONNABORTED
                    return $"Connection aborted (software caused connection abort)";
                case 0x80072746: // WSAECONNRESET
                    return $"Connection reset by remote peer (RST received)";
                case 0x80072747: // WSAENOBUFS
                    return $"No buffer space available (socket buffer exhausted)";
                case 0x8007274C: // WSAETIMEDOUT
                    return $"Connection timed out (remote host did not respond)";
                case 0x8007274D: // WSAECONNREFUSED
                    return $"Connection refused (port closed or service not running)";
                case 0x80072751: // WSAEHOSTUNREACH
                    return $"Host unreachable (no route to remote host)";
                case 0x80072AF9: // WSAHOST_NOT_FOUND
                    return $"Host not found (DNS resolution failed)";
                case 0x80072AFC: // WSANO_DATA
                    return $"No DNS data record for requested type";

                // WinHTTP / WinINet network errors
                case 0x80072EE2: // ERROR_INTERNET_TIMEOUT / ERROR_WINHTTP_TIMEOUT
                    return $"HTTP/network request timed out";
                case 0x80072EE7: // ERROR_INTERNET_NAME_NOT_RESOLVED / ERROR_WINHTTP_NAME_NOT_RESOLVED
                    return $"Hostname could not be resolved (DNS failure or no network)";
                case 0x80072EED: // ERROR_INTERNET_CANNOT_CONNECT / ERROR_WINHTTP_CANNOT_CONNECT
                    return $"Cannot connect to server (refused or unreachable)";
                case 0x80072EEE: // ERROR_INTERNET_CONNECTION_ABORTED
                    return $"Internet connection aborted (dropped mid-request)";
                case 0x80072EEF: // ERROR_INTERNET_CONNECTION_RESET
                    return $"Internet connection reset by server";
                case 0x80072EF3: // ERROR_INTERNET_INCORRECT_HANDLE_STATE
                    return $"WinINet handle in incorrect state (request lifecycle error)";
                case 0x80072F06: // ERROR_INTERNET_SEC_CERT_CN_INVALID
                    return $"SSL certificate common name mismatch (wrong domain on certificate)";
                case 0x80072F0D: // ERROR_INTERNET_INVALID_CA
                    return $"SSL certificate from untrusted authority (self-signed or expired CA)";
                case 0x80072F8F: // ERROR_INTERNET_DECRYPTION_FAILED
                    return $"TLS/SSL decryption failed (protocol mismatch or cipher unsupported)";

                // .NET / CLR exceptions
                case 0xE0434352: // CLR exception marker
                    return $".NET unhandled exception (CLR)";
                case 0xE0455843: // Fatal CLR error
                    return $".NET fatal execution engine error (EEException)";
                // FIX: 0xE0524F54 bytes decode to "ROT" and is not a verified FailFast marker.
                //      Environment.FailFast surfaces as COR_E_FAILFAST = 0x80131623.
                case 0x80131623: // COR_E_FAILFAST
                    return $"Environment.FailFast � application requested immediate termination";

                // .NET Framework / Core HRESULT codes
                case 0x80131500: return $".NET base Exception thrown and unhandled";
                case 0x80131501: return $".NET SystemException thrown and unhandled";
                case 0x80131502: return $".NET ArgumentOutOfRangeException";
                case 0x80131503: return $".NET ArrayTypeMismatchException";
                case 0x80131504: return $".NET ContextMarshalException (cross-AppDomain marshal failure)";
                case 0x80131506: return $".NET StackOverflowException";
                case 0x80131507: return $".NET ArithmeticException (overflow, divide by zero, etc.)";
                case 0x80131508: return $".NET DivideByZeroException";
                case 0x80131509: return $".NET InvalidCastException";
                case 0x8013150A: return $".NET NullReferenceException";
                case 0x8013150B: return $".NET OutOfMemoryException";
                case 0x8013150C: return $".NET OverflowException";
                case 0x8013150D: return $".NET FileNotFoundException";
                case 0x8013150E: return $".NET IOException";
                case 0x80131510: return $".NET TypeLoadException (missing type or assembly binding failure)";
                case 0x80131513: return $".NET IndexOutOfRangeException";
                case 0x80131515: return $".NET InvalidOperationException";
                case 0x80131516: return $".NET SecurityException (CAS / permission denied)";
                case 0x80131517: return $".NET SerializationException";
                case 0x80131519: return $".NET ThreadAbortException (Thread.Abort called � Framework only)";
                case 0x8013151A: return $".NET ThreadInterruptedException (Thread.Interrupt called)";
                case 0x8013151B: return $".NET ThreadStateException (invalid thread state)";
                case 0x8013151D: return $".NET EntryPointNotFoundException (P/Invoke or reflection failure)";
                case 0x80131522: return $".NET BadImageFormatException (wrong bitness or corrupt assembly)";
                case 0x80131523: return $".NET MethodAccessException (reflection or cross-assembly access denied)";
                case 0x80131524: return $".NET FieldAccessException";
                case 0x80131534: return $".NET MissingFieldException";
                case 0x80131535: return $".NET MissingMethodException";
                case 0x80131536: return $".NET MissingMemberException";
                case 0x80131537: return $".NET NotImplementedException";
                case 0x80131538: return $".NET NotSupportedException";
                case 0x80131539: return $".NET ObjectDisposedException";
                case 0x80131543: return $".NET AmbiguousMatchException (reflection)";
                case 0x80131577: return $".NET KeyNotFoundException";
                case 0x80131578: return $".NET InsufficientMemoryException";
                case 0x8013157B: return $".NET PlatformNotSupportedException";
                case 0x8013157D: return $".NET TimeoutException";
                case 0x80131620: return $".NET AppDomainUnloadedException (Framework only)";
                case 0x80131621: return $".NET RemotingException (Framework only � cross-AppDomain channel failure)";
                // REMOVED: 0x80006EE7 was a typo/duplicate of 0x80072EE7 (already listed above in WinHTTP section).
                //          .NET WebException name-resolution failures map to the WSA/WinHTTP codes, not a 0x80006xxx HRESULT.
                // REMOVED: 0x80133743 is not a real HRESULT. .NET SocketException surfaces as a Win32Exception
                //          carrying the underlying WSA error code (e.g. WSAENETDOWN = 0x80072742, listed above).

                // .NET runtime hosting / startup failures
                case 0x80008081: return $".NET runtime failed to load (shim error)";
                case 0x80008082: return $".NET runtime export not found (version mismatch)";
                case 0x80008083: return $".NET install root not found (runtime not installed)";
                case 0x80008091: return $".NET legacy runtime already bound (mixed version conflict)";
                case 0x80131022: return $".NET assembly requires a newer runtime version";
                case 0x80131044: return $".NET reference assembly cannot be loaded for execution";

                default:
                    if (code >= 0x80000000 && code <= 0x8FFFFFFF)
                        return $"NTSTATUS warning";
                    if (code >= 0xC0000000 && code <= 0xCFFFFFFF)
                        return $"NTSTATUS exception";
                    if (code >= 0xE0000000 && code <= 0xEFFFFFFF)
                        return $"Application exception";
                    if (exitCode < 0)
                        return $"Abnormal termination";
                    return $"Exit code {exitCode}";
            }
        }

        /// <summary>
        /// Builds a descriptive crash message combining exit code classification and zombie state.
        /// Used in log messages and health notifications to provide actionable context.
        /// </summary>
        private static string BuildCrashDescription(int? exitCode, bool hadZombies)
        {
            string reason;
            if (exitCode.HasValue)
            {
                reason = ClassifyExitCode(exitCode.Value);
                if (exitCode.Value != 0)
                {
                    reason = reason + " (exit code " + FormatExitCode(exitCode.Value) + ")";
                }
            }
            else
            {
                reason = "Process terminated (exit code unavailable)";
            }

            if (hadZombies)
            {
                reason = reason + " + zombie process(es) cleaned up";
            }

            return reason;
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
                        string newLauncherPath = editForm.NewLauncherPath;

                        // Check if directory changed and if the new path already exists in this group
                        if (newPath != app.Directory)
                        {
                            if (_storageService.ApplicationExistsInGroup(newPath, _selectedGroupId))
                            {
                                string newAppName = Path.GetFileNameWithoutExtension(newPath);
                                SimpleLogger.Warn("EditButton_Click @ Form1.cs", $"Duplicate application detected: {newAppName} at {newPath}");
                                MessageBoxHelper.ShowWarning(this,
                                    $"Application '{newAppName}' already exists in this group.\n\nPath: {newAppName}");
                                return;
                            }

                            string oldName = app.AppName;
                            app.AppName = Path.GetFileNameWithoutExtension(newPath);
                            app.Directory = newPath;

                            selectedItem.SubItems[1].Text = app.AppName;
                            selectedItem.SubItems[2].Text = app.Directory;

                            SimpleLogger.Info("EditButton_Click @ Form1.cs", $"Edited application path: {oldName} -> {app.AppName}");
                        }

                        // Update launcher path
                        string oldLauncher = app.LauncherPath;
                        app.LauncherPath = string.IsNullOrEmpty(newLauncherPath) ? null : newLauncherPath;
                        if (oldLauncher != app.LauncherPath)
                        {
                            SimpleLogger.Info("EditButton_Click @ Form1.cs",
                                $"Updated '{app.AppName}' launcher: {(string.IsNullOrEmpty(oldLauncher) ? "(none)" : Path.GetFileName(oldLauncher))} -> {(string.IsNullOrEmpty(app.LauncherPath) ? "(none)" : Path.GetFileName(app.LauncherPath))}");
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
                            $"Updated '{app.AppName}': KeepOpen={app.KeepOpen}, StartDelay={app.StartDelaySeconds}s, " +
                            $"StartupDelay={app.StartupDelaySeconds}s, Launcher={(!string.IsNullOrEmpty(app.LauncherPath) ? Path.GetFileName(app.LauncherPath) : "(none)")}");

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
                    _notifiedHealthIssues.Remove(app.Index);
                    _pendingStop.Remove(app.Index);
                    _pendingRestart.Remove(app.Index);
                    _failedApps.Remove(app.Index);
                    _startGracePeriod.Remove(app.Index);
                    _stableRunCheck.Remove(app.Index);
                    _notRespondingTracking.Remove(app.Index);
                    _cpuTimeSamples.Remove(app.Index);
                    _highCpuStreak.Remove(app.Index);
                    _errorDialogTracking.Remove(app.Index);
                    _knownWindowTitles.Remove(app.Index);
                    _titleChangeTracking.Remove(app.Index);
                    _forceKillReason.Remove(app.Index);
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
            // Validate the primary exe path always exists (needed for process detection)
            if (string.IsNullOrEmpty(app.Directory) || !File.Exists(app.Directory))
            {
                MessageBoxHelper.ShowError(this, $"Application file not found: {app.Directory ?? "(empty)"}");
                return false;
            }

            // If a launcher is configured, validate it exists too
            if (!string.IsNullOrEmpty(app.LauncherPath) && !File.Exists(app.LauncherPath))
            {
                MessageBoxHelper.ShowError(this, $"Launcher file not found: {app.LauncherPath}");
                return false;
            }

            if (_processManager.IsApplicationRunning(app))
            {
                // Check if the process is running because another group started it
                var siblings = _storageService.FindOtherEntriesWithSamePath(app.Directory, app.Index);
                foreach (var sibling in siblings)
                {
                    if (_authorizedApps.Contains(sibling.Index))
                    {
                        MessageBoxHelper.ShowWarning(this,
                            $"'{app.AppName}' is already running in group '{sibling.GroupId}'.\n\n" +
                            "You must stop it in the other group first before starting it here.");
                        return false;
                    }
                }

                SimpleLogger.Info("StartSingleApplication @ Form1.cs", $"'{app.AppName}' is already running, skipping");
                return true; // Already running is not a failure
            }

            if (_processManager.StartApplication(app))
            {
                _authorizedApps.Add(app.Index);
                _notifiedUnauthorized.Remove(app.Index);
                _notifiedHealthIssues.Remove(app.Index);
                _pendingStop.Remove(app.Index);
                _failedApps.Remove(app.Index);
                _forceKillReason.Remove(app.Index);

                // Grant a grace period so the watchdog doesn't treat the app as crashed
                // before its main window has had time to appear
                _startGracePeriod[app.Index] = DateTime.Now.AddSeconds(_settingsService.StartGracePeriodSeconds);

                // Schedule a stable run check � retry count resets after this period
                int stablePeriod = Math.Max(app.StableRunPeriodSeconds, 5);
                _stableRunCheck[app.Index] = DateTime.Now.AddSeconds(stablePeriod);

                DateTime now = DateTime.Now;
                // Do NOT reset retryCount here � it will be reset only after the app
                // runs stably for the configured StableRunPeriodSeconds.
                // Retry count resets only on: (1) manual start by user, or (2) stable run period.
                _storageService.UpdateApplicationFields(app.Index, isRunning: true, lastStart: now);

                if (item != null)
                {
                    item.SubItems[3].Text = "Starting...";
                    item.SubItems[6].Text = "0";
                    item.SubItems[7].Text = app.GetLastStartDisplay();
                    item.SubItems[9].Text = "�";
                    item.ForeColor = Color.DarkOrange;

                    var tagApp = item.Tag as ManagedApplication;
                    if (tagApp != null)
                    {
                        tagApp.LastStart = app.LastStart;
                        tagApp.IsRunning = true;
                        tagApp.RetryCount = 0;
                        tagApp.LastExitCode = null;
                    }
                }


                // Refresh group indicator immediately
                UpdateGroupIndicators(_storageService.GetAllApplicationsReadOnly());
                SimpleLogger.Info("StartSingleApplication @ Form1.cs",
                    $"Started '{app.AppName}' successfully");
                return true;
            }
            else
            {
                SimpleLogger.Error("StartSingleApplication @ Form1.cs",
                    $"Failed to start '{app.AppName}'");

                _failedApps.Add(app.Index);

                if (item != null)
                {
                    item.SubItems[3].Text = "Failed";
                    item.ForeColor = Color.Red;
                }
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

                // Check if this app is running from another group (not authorized in this group)
                if (!_authorizedApps.Contains(app.Index))
                {
                    var authorizedSibling = _storageService.FindAuthorizedSibling(app.Directory, app.Index, _authorizedApps);
                    if (authorizedSibling != null)
                    {
                        var siblingGroup = _storageService.GetGroup(authorizedSibling.GroupId);
                        string originGroupName = siblingGroup != null ? siblingGroup.GroupName : $"Group {authorizedSibling.GroupId}";
                        MessageBoxHelper.ShowWarning(this,
                            $"'{app.AppName}' was started from group '{originGroupName}'.\n\n" +
                            "Please stop it from its origin group.");
                        SimpleLogger.Info("StopButton_Click @ Form1.cs",
                            $"Blocked stop of '{app.AppName}' � running from group '{originGroupName}'");
                        return;
                    }
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
            _notifiedHealthIssues.Remove(app.Index);
            _startGracePeriod.Remove(app.Index);
            _stableRunCheck.Remove(app.Index);
            _notRespondingTracking.Remove(app.Index);
            _cpuTimeSamples.Remove(app.Index);
            _highCpuStreak.Remove(app.Index);
            _errorDialogTracking.Remove(app.Index);
            _knownWindowTitles.Remove(app.Index);
            _titleChangeTracking.Remove(app.Index);
            _forceKillReason.Remove(app.Index);

            // Show "Stopping" immediately so the user gets visual feedback
            if (item != null)
            {
                item.SubItems[3].Text = "Stopping...";
                item.ForeColor = Color.DarkOrange;
            }

            // StopApplication now captures the exit code internally after the process exits
            int? exitCode;
            if (_processManager.StopApplication(app, out exitCode))
            {
                app.LastStop = DateTime.Now;
                app.IsRunning = false;
                // Do NOT overwrite LastExitCode on intentional user stop.
                // The previous exit code (from a crash or unexpected exit) is far more
                // useful for troubleshooting than the exit code from a graceful/forced stop
                // (which is typically 0 or a kill code). Preserve the existing value.
                _storageService.UpdateApplication(app);

                _pendingStop.Remove(app.Index);

                if (item != null)
                {
                    item.SubItems[3].Text = "Stopped";
                    item.SubItems[8].Text = app.GetLastStopDisplay();
                    // Keep showing the existing last exit code (don't overwrite with stop's exit code)
                    item.SubItems[9].Text = FormatExitCodeDisplay(app.LastExitCode);
                    item.ForeColor = Color.Black;
                }

                // Refresh group indicator immediately
                UpdateGroupIndicators(_storageService.GetAllApplicationsReadOnly());

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
                    // Sequential launch with startup delays � use a timer-based approach
                    // to avoid blocking the UI thread
                    StartAllSequential(appsToStart, alreadyRunning);
                    return;
                }

                // No delays configured � launch all immediately (existing behavior)
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
                // Dispose and clear the tracked sequential timer
                if (_sequentialLaunchTimer != null)
                {
                    _sequentialLaunchTimer.Stop();
                    _sequentialLaunchTimer.Dispose();
                    _sequentialLaunchTimer = null;
                }

                // Clear all sequential start markers
                foreach (var kvp in appsToStart)
                {
                    _pendingSequentialStart.Remove(kvp.Key.Index);
                    _pendingSequentialStartAt.Remove(kvp.Key.Index);
                }

                StartAllButton.Enabled = true;
                StopAllButton.Enabled = true;

                string msg = $"Started: {started}";
                if (alreadyRunning > 0) msg += $", Already running: {alreadyRunning}";
                if (failed > 0) msg += $", Failed: {failed}";

                SimpleLogger.Info("StartAllSequential @ Form1.cs", $"Start All (sequential) for group {_selectedGroupId}: {msg}");

                if (failed > 0)
                    MessageBoxHelper.ShowWarning(this, msg);
                else
                    MessageBoxHelper.ShowSuccess(this, msg);
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
                _pendingSequentialStartAt.Remove(app.Index);

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
                        if (_sequentialLaunchTimer != null)
                        {
                            _sequentialLaunchTimer.Stop();
                            _sequentialLaunchTimer.Dispose();
                        }

                        _sequentialLaunchTimer = new Timer();
                        _sequentialLaunchTimer.Interval = 1000;
                        _sequentialLaunchTimer.Tick += (s, ev) =>
                        {
                            secondsLeft--;
                            if (secondsLeft <= 0 || _isClosing)
                            {
                                if (_sequentialLaunchTimer != null)
                                {
                                    _sequentialLaunchTimer.Stop();
                                }
                                launchNext();
                            }
                            else if (nextItem != null)
                            {
                                nextItem.SubItems[3].Text = $"Waiting ({secondsLeft}s)";
                            }
                        };
                        _sequentialLaunchTimer.Start();
                    }
                    else
                    {
                        // No delay � launch immediately
                        launchNext();
                    }
                }
                else
                {
                    // No more apps � finalize
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
                int skippedCrossGroup = 0;

                for (int i = AppListView.Items.Count - 1; i >= 0; i--)
                {
                    ListViewItem item = AppListView.Items[i];
                    var app = item.Tag as ManagedApplication;
                    if (app == null) continue;

                    // Cancel any pending restart since user is intentionally stopping all
                    _pendingRestart.Remove(app.Index);
                    _failedApps.Remove(app.Index);
                    _stableRunCheck.Remove(app.Index);
                    _notRespondingTracking.Remove(app.Index);

                    if (_processManager.IsApplicationRunning(app))
                    {
                        // Skip apps that are running from another group
                        if (!_authorizedApps.Contains(app.Index))
                        {
                            var authorizedSibling = _storageService.FindAuthorizedSibling(app.Directory, app.Index, _authorizedApps);
                            if (authorizedSibling != null)
                            {
                                skippedCrossGroup++;
                                SimpleLogger.Info("StopAllButton_Click @ Form1.cs",
                                    $"Skipped '{app.AppName}' � running from another group");
                                continue;
                            }
                        }

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
                if (skippedCrossGroup > 0) msg += $", Skipped (other group): {skippedCrossGroup}";
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
                using (var dialog = new SettingsDialog(_settingsService, _storageService.GetAllGroups()))
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        _settingsService.SaveAll(
                            dialog.StationName,
                            dialog.RunOnStartup,
                            dialog.StatusPollIntervalMs,
                            dialog.StartGracePeriodSeconds,
                            dialog.GcCollectIntervalMinutes,
                            dialog.StorageSaveIntervalSeconds,
                            dialog.LogFlushIntervalSeconds,
                            dialog.LogBufferSize,
                            dialog.LogRetentionDays);

                        // Apply settings to running services
                        ApplySettingsToServices();

                        // Update the status poll timer interval
                        if (_statusUpdateTimer != null)
                        {
                            _statusUpdateTimer.Interval = _settingsService.StatusPollIntervalMs;
                        }

                        UpdateStationNameDisplay();

                        SimpleLogger.Info("SettingsButton_Click @ Form1.cs",
                            $"Updated settings: StationName='{dialog.StationName}', RunOnStartup={dialog.RunOnStartup}, " +
                            $"StatusPollIntervalMs={dialog.StatusPollIntervalMs}, StartGracePeriodSeconds={dialog.StartGracePeriodSeconds}, " +
                            $"GcCollectIntervalMinutes={dialog.GcCollectIntervalMinutes}, StorageSaveIntervalSeconds={dialog.StorageSaveIntervalSeconds}, " +
                            $"LogFlushIntervalSeconds={dialog.LogFlushIntervalSeconds}, LogBufferSize={dialog.LogBufferSize}, " +
                            $"LogRetentionDays={dialog.LogRetentionDays}");
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("SettingsButton_Click @ Form1.cs", $"Error opening settings: {ex.Message}");
                MessageBoxHelper.ShowError(this, $"Error opening settings: {ex.Message}");
            }
        }

        private void UserGuideButton_Click(object sender, EventArgs e)
        {
            try
            {
                string guidePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Intelligent Mutex Execution Environment - User Guide Presntation.pdf");

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
            // If close already in progress, skip prompt
            if (!_isClosing && e.CloseReason == CloseReason.UserClosing)
            {
                // Show a confirmation that offers Minimize to Tray / Close / Cancel
                // Only show once per session unless user explicitly chooses Close
                var promptMsg = "Do you want to minimize IMEE to the system tray or close the application?\n\n" +
                                "Choose 'Yes' to keep it running in the background.\n" +
                                "Choose 'No' to exit and stop monitoring.";


                // Build a custom dialog using MessageBox buttons � map to choices
                var choice = MessageBox.Show(this, promptMsg, "Close Application?", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                // Yes = Minimize to Tray, No = Close, Cancel = Cancel
                if (choice == DialogResult.Yes)
                {
                    e.Cancel = true; // cancel the close
                    MinimizeToTray();
                    return;
                }
                else if (choice == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                // else: DialogResult.No => proceed with close
            }

            // Set _isClosing immediately to prevent all timers from firing during teardown
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

            if (_sequentialLaunchTimer != null)
            {
                _sequentialLaunchTimer.Stop();
                _sequentialLaunchTimer.Dispose();
                _sequentialLaunchTimer = null;
            }

            // Flush any pending data before closing
            if (_storageService != null)
            {
                _storageService.FlushPendingChanges();
            }

            // Dispose all tracked Process handles
            if (_processManager != null)
            {
                _processManager.DisposeAllTrackedHandles();
            }

            base.OnFormClosing(e);
        }

        private void GroupListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();

            if (e.Index < 0)
                return;

            var group = GroupListBox.Items[e.Index] as ApplicationGroup;
            if (group == null)
                return;

            // Lookup the cached display status, default to gray if not found
            GroupDisplayStatus status;
            if (!_groupDisplayStatus.TryGetValue(group.GroupId, out status))
            {
                status = new GroupDisplayStatus { IndicatorColor = Color.Gray, Suffix = "" };
            }

            // Layout: [4px pad][8px circle][6px gap] = 18px reserved for indicator
            int circleSize = 8;
            int indicatorMargin = 18;
            int circleX = e.Bounds.Left + 4;
            int circleY = e.Bounds.Top + (e.Bounds.Height - circleSize) / 2;

            bool isSelected = (e.State & DrawItemState.Selected) != 0;

            // Paint indicator area with control background (never highlighted)
            using (var indicatorBgBrush = new SolidBrush(GroupListBox.BackColor))
            {
                e.Graphics.FillRectangle(indicatorBgBrush, e.Bounds.Left, e.Bounds.Top, indicatorMargin, e.Bounds.Height);
            }

            // Paint text area with selection highlight or normal background
            Rectangle textAreaRect = new Rectangle(e.Bounds.Left + indicatorMargin, e.Bounds.Top,
                e.Bounds.Width - indicatorMargin, e.Bounds.Height);
            Color textAreaBg = isSelected ? SystemColors.Highlight : GroupListBox.BackColor;
            using (var textBgBrush = new SolidBrush(textAreaBg))
            {
                e.Graphics.FillRectangle(textBgBrush, textAreaRect);
            }

            // Draw status indicator circle
            using (var brush = new SolidBrush(status.IndicatorColor))
            {
                e.Graphics.FillEllipse(brush, circleX, circleY, circleSize, circleSize);
            }

            // Measure suffix width so we can reserve space for it on the right
            int suffixWidth = 0;
            if (!string.IsNullOrEmpty(status.Suffix))
            {
                var suffixSize = e.Graphics.MeasureString(status.Suffix, e.Font);
                suffixWidth = (int)Math.Ceiling(suffixSize.Width) + 4; // 4px right padding
            }

            // Draw group name text (left-aligned, truncated before suffix area)
            int textX = e.Bounds.Left + indicatorMargin + 2;
            int textAvailableWidth = e.Bounds.Width - (textX - e.Bounds.Left) - suffixWidth;
            Color textColor = isSelected ? SystemColors.HighlightText : e.ForeColor;

            using (var textBrush = new SolidBrush(textColor))
            {
                var nameRect = new RectangleF(textX, e.Bounds.Top, Math.Max(textAvailableWidth, 0), e.Bounds.Height);
                e.Graphics.DrawString(group.GroupName ?? "(unnamed)", e.Font, textBrush, nameRect, _groupNameFormat);
            }

            // Draw suffix text (right-aligned)
            if (!string.IsNullOrEmpty(status.Suffix))
            {
                Color suffixColor = isSelected ? SystemColors.HighlightText : Color.FromArgb(120, 120, 120);
                using (var suffixBrush = new SolidBrush(suffixColor))
                {
                    var suffixRect = new RectangleF(e.Bounds.Right - suffixWidth, e.Bounds.Top, suffixWidth, e.Bounds.Height);
                    e.Graphics.DrawString(status.Suffix, e.Font, suffixBrush, suffixRect, _groupSuffixFormat);
                }
            }

            // Draw focus rectangle only around the text area
            if ((e.State & DrawItemState.Focus) != 0)
            {
                ControlPaint.DrawFocusRectangle(e.Graphics, textAreaRect);
            }
        }

        /// <summary>
        /// Updates the cached group display status based on current app states and all
        /// transitional tracking sets. Only invalidates the GroupListBox if any group's
        /// visual representation actually changed.
        /// </summary>
        private void UpdateGroupIndicators(IList<ManagedApplication> allApps)
        {
            // Build per-group counters in a single pass over all apps
            // Using Dictionary instead of LINQ grouping to avoid allocations
            var groupStats = new Dictionary<int, GroupCounts>();

            foreach (var app in allApps)
            {
                GroupCounts counts;
                if (!groupStats.TryGetValue(app.GroupId, out counts))
                {
                    counts = new GroupCounts();
                    groupStats[app.GroupId] = counts;
                }

                counts.Total++;

                if (_failedApps.Contains(app.Index))
                {
                    counts.Failed++;
                }
                else if (_pendingStop.Contains(app.Index)
                    || _startGracePeriod.ContainsKey(app.Index)
                    || _pendingRestart.ContainsKey(app.Index)
                    || _pendingSequentialStart.Contains(app.Index))
                {
                    counts.Transitional++;
                    // Also count as running if the app is still marked running (stopping scenario)
                    if (app.IsRunning)
                        counts.Running++;
                }
                else if (app.IsRunning)
                {
                    counts.Running++;
                }
            }

            bool changed = false;

            for (int i = 0; i < GroupListBox.Items.Count; i++)
            {
                var group = GroupListBox.Items[i] as ApplicationGroup;
                if (group == null) continue;

                GroupDisplayStatus newStatus;
                GroupCounts c;
                if (!groupStats.TryGetValue(group.GroupId, out c) || c.Total == 0)
                {
                    // Empty group � gray, no suffix
                    newStatus = new GroupDisplayStatus { IndicatorColor = Color.Gray, Suffix = "" };
                }
                else if (c.Failed == 0 && c.Running == c.Total)
                {
                    // All apps running (even during startup grace periods) — green
                    newStatus = new GroupDisplayStatus
                    {
                        IndicatorColor = Color.Green,
                        Suffix = c.Running + " / " + c.Total
                    };
                }
                else if (c.Failed > 0)
                {
                    // Has failed apps� red indicator
                    if (c.Failed >= c.Total)
                    {
                        // All failed � no suffix needed
                        newStatus = new GroupDisplayStatus { IndicatorColor = Color.Red, Suffix = "" };
                    }
                    else
                    {
                        newStatus = new GroupDisplayStatus
                        {
                            IndicatorColor = Color.Red,
                            Suffix = c.Failed + " / " + c.Total
                        };
                    }
                }
                else if (c.Transitional > 0)
                {
                    // Has transitional apps � orange indicator, no suffix
                    newStatus = new GroupDisplayStatus { IndicatorColor = Color.DarkOrange, Suffix = "" };
                }
                else if (c.Running > 0)
                {
                    // Has running apps � green indicator
                    newStatus = new GroupDisplayStatus
                    {
                        IndicatorColor = Color.Green,
                        Suffix = c.Running + " / " + c.Total
                    };
                }
                else
                {
                    // All stopped � gray, no suffix
                    newStatus = new GroupDisplayStatus { IndicatorColor = Color.Gray, Suffix = "" };
                }

                GroupDisplayStatus oldStatus;
                if (!_groupDisplayStatus.TryGetValue(group.GroupId, out oldStatus) || !oldStatus.Equals(newStatus))
                {
                    _groupDisplayStatus[group.GroupId] = newStatus;
                    changed = true;
                }
            }

            if (changed)
            {
                GroupListBox.Invalidate();
            }
        }

        /// <summary>
        /// Rebuilds the reusable ListView index dictionary from current AppListView items.
        /// Clears and repopulates to avoid allocating a new dictionary each time.
        /// </summary>
        private void RebuildListViewIndex(Dictionary<int, ListViewItem> index)
        {
            index.Clear();
            foreach (ListViewItem lvi in AppListView.Items)
            {
                var tagApp = lvi.Tag as ManagedApplication;
                if (tagApp != null)
                {
                    index[tagApp.Index] = lvi;
                }
            }
        }

        /// <summary>
        /// Lightweight counter bucket for per-group status aggregation.
        /// Used as a reference type (class) so dictionary lookups can mutate in place.
        /// </summary>
        private class GroupCounts
        {
            public int Total;
            public int Running;
            public int Failed;
            public int Transitional;
        }

        // User preference: show minimize to tray prompt on close only once per session

        /// <summary>
        /// Restores the main window from tray.
        /// </summary>
        private void RestoreFromTray()
        {
            try
            {
                notifyIcon.Visible = false;

                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.BringToFront();
                this.Activate();
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("RestoreFromTray @ Form1.cs", $"Error restoring from tray: {ex.Message}");
            }
        }

        /// <summary>
        /// Minimizes the form to the tray icon.
        /// </summary>
        private void MinimizeToTray()
        {
            try
            {
                notifyIcon.Visible = true;
                notifyIcon.ShowBalloonTip(1000, "IMEE", "Application minimized to tray.", ToolTipIcon.Info);

                this.Hide();
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("MinimizeToTray @ Form1.cs", $"Error minimizing to tray: {ex.Message}");
            }
        }

        private void NotifyIcon_DoubleClick(object sender, EventArgs e)
        {
            RestoreFromTray();
        }

        private void TrayRestoreMenuItem_Click(object sender, EventArgs e)
        {
            RestoreFromTray();
        }

        private void TrayExitMenuItem_Click(object sender, EventArgs e)
        {
            // Ask user for confirmation and then close application
            var result = MessageBoxHelper.ShowQuestion(this, "Exit IMEE and stop monitoring?", "Confirm Exit");
            if (result == DialogResult.Yes)
            {
                // Ensure notify icon hidden to prevent orphaned icon
                try { if (notifyIcon != null) notifyIcon.Visible = false; } catch { }
                Application.Exit();
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Program.WM_SHOWFIRSTINSTANCE)
            {
                SimpleLogger.Info("WndProc @ Form1.cs", "Received show request from second instance � restoring window");
                RestoreFromTray();
                return;
            }
            base.WndProc(ref m);
        }

        // ===== Drag-to-Reorder App List =====

        private void AppListView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            AppListView.DoDragDrop(e.Item, DragDropEffects.Move);
        }

        private void AppListView_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(typeof(ListViewItem))
                ? DragDropEffects.Move
                : DragDropEffects.None;
        }

        private void AppListView_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(ListViewItem)))
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            e.Effect = DragDropEffects.Move;

            Point clientPoint = AppListView.PointToClient(new Point(e.X, e.Y));
            int targetIndex = AppListView.InsertionMark.NearestIndex(clientPoint);
            if (targetIndex >= 0)
            {
                Rectangle bounds = AppListView.GetItemRect(targetIndex);
                AppListView.InsertionMark.AppearsAfterItem = clientPoint.Y > bounds.Top + bounds.Height / 2;
                AppListView.InsertionMark.Index = targetIndex;
            }
            else
            {
                AppListView.InsertionMark.Index = -1;
            }
        }

        private void AppListView_DragDrop(object sender, DragEventArgs e)
        {
            AppListView.InsertionMark.Index = -1;

            if (!e.Data.GetDataPresent(typeof(ListViewItem)))
                return;

            var draggedItem = (ListViewItem)e.Data.GetData(typeof(ListViewItem));
            Point clientPoint = AppListView.PointToClient(new Point(e.X, e.Y));
            int targetIndex = AppListView.InsertionMark.NearestIndex(clientPoint);

            if (targetIndex < 0)
                targetIndex = AppListView.Items.Count - 1;

            int clampedTarget = Math.Max(0, Math.Min(targetIndex, AppListView.Items.Count - 1));
            Rectangle bounds = AppListView.GetItemRect(clampedTarget);
            bool insertAfter = clientPoint.Y > bounds.Top + bounds.Height / 2;

            int draggedIndex = draggedItem.Index;
            int insertAt = insertAfter ? targetIndex + 1 : targetIndex;

            if (draggedIndex == insertAt || draggedIndex == insertAt - 1)
                return;

            var clone = (ListViewItem)draggedItem.Clone();
            AppListView.Items.RemoveAt(draggedIndex);

            if (draggedIndex < insertAt) insertAt--;
            insertAt = Math.Max(0, Math.Min(insertAt, AppListView.Items.Count));

            AppListView.Items.Insert(insertAt, clone);
            AppListView.SelectedItems.Clear();
            clone.Selected = true;

            if (_selectedGroupId > 0)
                PersistAppListOrder(_selectedGroupId);
        }

        private void PersistAppListOrder(int groupId)
        {
            var orderedIndices = new List<int>();
            foreach (ListViewItem item in AppListView.Items)
            {
                var app = item.Tag as ManagedApplication;
                if (app != null) orderedIndices.Add(app.Index);
            }
            _storageService.UpdateApplicationSortOrders(groupId, orderedIndices);
        }
    }
}
