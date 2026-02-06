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
        // Prevents re-entrant timer ticks while a tick is still executing
        private bool _isUpdatingStatuses;
        // Currently selected group ID (-1 means no group selected)
        private int _selectedGroupId = -1;
        // Prevents timer ticks from running after form begins closing
        private bool _isClosing;

        public Main()
        {
            InitializeComponent();
            try
            {
                InitializeServices();
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
        }

        /// <summary>
        /// On startup, terminate any managed apps that were already running
        /// since they were not launched through Instance Manager.
        /// </summary>
        private void TerminateAlreadyRunningApps()
        {
            try
            {
                var apps = _storageService.GetAllApplications();
                List<string> terminatedApps = new List<string>();

                foreach (var app in apps)
                {
                    if (string.IsNullOrEmpty(app.Directory))
                        continue;

                    if (_processManager.IsApplicationRunning(app))
                    {
                        SimpleLogger.Warn("TerminateAlreadyRunningApps @ Form1.cs",
                            $"'{app.AppName}' was running before Instance Manager started - terminating");

                        _processManager.StopApplication(app);

                        app.LastStop = DateTime.Now;
                        app.IsRunning = false;
                        _storageService.UpdateApplication(app);

                        terminatedApps.Add(app.AppName ?? "(unknown)");
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
            catch (Exception ex)
            {
                SimpleLogger.Error("TerminateAlreadyRunningApps @ Form1.cs", $"Error during startup termination: {ex.Message}");
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
            item.SubItems.Add(app.AppName ?? "");
            item.SubItems.Add(app.Directory ?? "");
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
                // Update statuses for ALL managed apps (not just the visible group)
                // to enforce watchdog across all groups
                var allApps = _storageService.GetAllApplications();

                foreach (var app in allApps)
                {
                    if (string.IsNullOrEmpty(app.Directory))
                        continue;

                    // Find the corresponding ListView item if this app is in the currently displayed group
                    ListViewItem item = null;
                    if (app.GroupId == _selectedGroupId)
                    {
                        foreach (ListViewItem lvi in AppListView.Items)
                        {
                            var tagApp = lvi.Tag as ManagedApplication;
                            if (tagApp != null && tagApp.Index == app.Index)
                            {
                                item = lvi;
                                break;
                            }
                        }
                    }

                    // Skip watchdog checks for apps the user intentionally stopped
                    if (_pendingStop.Contains(app.Index))
                    {
                        bool stillRunning = _processManager.IsApplicationRunning(app);
                        if (!stillRunning)
                        {
                            _pendingStop.Remove(app.Index);
                            if (item != null && item.SubItems[3].Text == "Running")
                            {
                                item.SubItems[3].Text = "Stopped";
                                item.ForeColor = Color.Black;
                            }
                        }
                        continue;
                    }

                    bool wasRunning = app.IsRunning;
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
                        _storageService.UpdateApplication(app);

                        if (item != null)
                        {
                            item.SubItems[3].Text = "Stopped";
                            item.SubItems[5].Text = app.GetLastStopDisplay();
                            item.ForeColor = Color.Black;
                        }

                        SimpleLogger.Info("UpdateApplicationStatuses @ Form1.cs",
                            $"'{app.AppName}' was stopped externally");
                    }

                    if (app.IsRunning != isRunning)
                    {
                        app.IsRunning = isRunning;
                        _storageService.UpdateApplication(app);
                    }

                    if (item != null)
                    {
                        string statusText = isRunning ? "Running" : "Stopped";
                        if (item.SubItems[3].Text != statusText)
                        {
                            item.SubItems[3].Text = statusText;
                            item.ForeColor = isRunning ? Color.Green : Color.Black;
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

            // Update state
            app.IsRunning = false;

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

                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*";
                    dialog.Title = "Select New Application Path";
                    dialog.FileName = app.Directory;

                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        string newPath = dialog.FileName;

                        // Check if the new path is different and if it already exists in this group
                        if (newPath != app.Directory && _storageService.ApplicationExistsInGroup(newPath, _selectedGroupId))
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

                app.LastStart = DateTime.Now;
                app.IsRunning = true;
                _storageService.UpdateApplication(app);

                if (item != null)
                {
                    item.SubItems[3].Text = "Running";
                    item.SubItems[4].Text = app.GetLastStartDisplay();
                    item.ForeColor = Color.Green;
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

            if (_processManager.StopApplication(app))
            {
                app.LastStop = DateTime.Now;
                app.IsRunning = false;
                _storageService.UpdateApplication(app);

                _pendingStop.Remove(app.Index);

                if (item != null)
                {
                    item.SubItems[3].Text = "Stopped";
                    item.SubItems[5].Text = app.GetLastStopDisplay();
                    item.ForeColor = Color.Black;
                }

                return true;
            }
            else
            {
                _pendingStop.Remove(app.Index);
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

                int started = 0;
                int failed = 0;
                int alreadyRunning = 0;

                foreach (ListViewItem item in AppListView.Items)
                {
                    var app = item.Tag as ManagedApplication;
                    if (app == null) continue;

                    if (_processManager.IsApplicationRunning(app))
                    {
                        alreadyRunning++;
                        continue;
                    }

                    if (StartSingleApplication(app, item))
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

                foreach (ListViewItem item in AppListView.Items)
                {
                    var app = item.Tag as ManagedApplication;
                    if (app == null) continue;

                    if (!_processManager.IsApplicationRunning(app))
                    {
                        notRunning++;
                        continue;
                    }

                    _pendingStop.Add(app.Index);

                    if (StopSingleApplication(app, item))
                    {
                        stopped++;
                    }
                    else
                    {
                        failed++;
                    }
                }

                string msg = $"Stopped: {stopped}";
                if (notRunning > 0) msg += $", Already stopped: {notRunning}";
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

            base.OnFormClosing(e);

            SimpleLogger.Info("OnFormClosing @ Form1.cs", "Application closing");
        }
    }
}
