using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using IntelligentMutexExecutionEnvironment.Models;
using IntelligentMutexExecutionEnvironment.Utilities;

namespace IntelligentMutexExecutionEnvironment
{
    public class EditAppDialog : Form
    {
        private ManagedApplication _app;
        private TextBox _directoryTextBox;
        private Button _browseButton;
        private Button _openPathButton;
        private TextBox _launcherTextBox;
        private Button _launcherBrowseButton;
        private Button _launcherClearButton;
        private Button _launcherOpenButton;
        private CheckBox _keepOpenCheckBox;
        private NumericUpDown _startDelayNumeric;
        private NumericUpDown _maxRetriesNumeric;
        private NumericUpDown _startupDelayNumeric;
        private NumericUpDown _stableRunNumeric;
        private NumericUpDown _notRespondingTimeoutNumeric;
        private NumericUpDown _memoryLimitNumeric;
        private CheckBox _detectTitleChangeCheckBox;
        private Button _resetCrashButton;
        private Button _resetRetryButton;
        private Label _crashCountLabel;
        private Label _retryCountLabel;
        private Label _lastExitCodeLabel;
        private Button _copyExitCodeButton;
        private Button _okButton;
        private Button _cancelButton;

        // Shared font instances to avoid creating duplicate Font objects
        private Font _normalFont;
        private Font _boldFont;
        private Font _smallBoldFont;
        private Font _buttonFont;

        // Original values captured at dialog open for change detection logging
        private int _originalCrashCount;
        private int _originalRetryCount;

        public string NewDirectory { get { return _directoryTextBox.Text.Trim(); } }
        public string NewLauncherPath { get { return _launcherTextBox.Text.Trim(); } }

        public EditAppDialog(ManagedApplication app)
        {
            _app = app;

            // Capture original statistics values before any reset buttons are clicked
            _originalCrashCount = app.CrashCount;
            _originalRetryCount = app.RetryCount;

            // Create shared fonts once
            _normalFont = new Font("AUMOVIO Screen", 9F);
            _boldFont = new Font("AUMOVIO Screen", 9F, FontStyle.Bold);
            _smallBoldFont = new Font("AUMOVIO Screen", 7F, FontStyle.Bold);
            _buttonFont = new Font("AUMOVIO Screen", 8F, FontStyle.Bold);

            InitializeControls();
        }

        private void InitializeControls()
        {
            this.Text = $"Edit - {_app.AppName}";
            this.Size = new Size(600, 740);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = _normalFont;

            int labelX = 20;
            int controlX = 170;
            int y = 20;
            int rowHeight = 32;

            // --- Directory Section ---
            var dirSectionLabel = new Label
            {
                Text = "Application Path",
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = _boldFont
            };
            this.Controls.Add(dirSectionLabel);

            y += 22;

            _directoryTextBox = new TextBox
            {
                Text = _app.Directory ?? "",
                Location = new Point(labelX, y),
                // Wider textbox to use dialog width better
                Size = new Size(controlX + 220, 23),
                Font = _normalFont
            };
            _browseButton = new Button
            {
                Text = "Browse",
                // position buttons to the right of the widened textbox
                Location = new Point(labelX + (controlX + 220) + 10, y - 1),
                Size = new Size(75, 25),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Font = _smallBoldFont
            };
            _browseButton.Click += BrowseButton_Click;
            _openPathButton = new Button
            {
                Text = "Open",
                Location = new Point(labelX + (controlX + 220) + 95, y - 1),
                Size = new Size(75, 25),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(155, 89, 182),
                ForeColor = Color.White,
                Font = _smallBoldFont
            };
            _openPathButton.Click += OpenPathButton_Click;
            this.Controls.Add(_directoryTextBox);
            this.Controls.Add(_browseButton);
            this.Controls.Add(_openPathButton);

            y += rowHeight + 10;

            // --- Launcher Script/Exe Section ---
            var launcherSectionLabel = new Label
            {
                Text = "Launcher Script / Exe (Optional)",
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = _boldFont
            };
            this.Controls.Add(launcherSectionLabel);

            y += 22;

            var launcherHint = new Label
            {
                Text = "If set, IMEE will launch via this script/exe instead of the application path directly. Process detection still uses the application path above.",
                Location = new Point(labelX, y),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = _normalFont,
                MaximumSize = new Size(this.ClientSize.Width - (labelX * 2), 0)
            };
            this.Controls.Add(launcherHint);

            y += launcherHint.PreferredSize.Height + 6;

            _launcherTextBox = new TextBox
            {
                Text = _app.LauncherPath ?? "",
                Location = new Point(labelX, y),
                Size = new Size(controlX + 220, 23),
                Font = _normalFont
            };
            _launcherBrowseButton = new Button
            {
                Text = "Browse",
                Location = new Point(labelX + (controlX + 220) + 10, y - 1),
                Size = new Size(55, 25),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Font = _smallBoldFont
            };
            _launcherBrowseButton.Click += LauncherBrowseButton_Click;
            _launcherOpenButton = new Button
            {
                Text = "Open",
                Location = new Point(labelX + (controlX + 220) + 70, y - 1),
                Size = new Size(50, 25),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(155, 89, 182),
                ForeColor = Color.White,
                Font = _smallBoldFont
            };
            _launcherOpenButton.Click += LauncherOpenButton_Click;
            _launcherClearButton = new Button
            {
                Text = "Clear",
                Location = new Point(labelX + (controlX + 220) + 125, y - 1),
                Size = new Size(50, 25),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(192, 57, 43),
                ForeColor = Color.White,
                Font = _smallBoldFont
            };
            _launcherClearButton.Click += (s, ev) => { _launcherTextBox.Text = ""; };
            this.Controls.Add(_launcherTextBox);
            this.Controls.Add(_launcherBrowseButton);
            this.Controls.Add(_launcherOpenButton);
            this.Controls.Add(_launcherClearButton);

            y += rowHeight + 10;

            // --- Startup Settings Section ---
            var startupSectionLabel = new Label
            {
                Text = "Startup Settings",
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = _boldFont
            };
            this.Controls.Add(startupSectionLabel);

            y += 22;

            // Startup Delay
            var startupDelayLabel = new Label
            {
                Text = "Startup Delay (sec):",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _startupDelayNumeric = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 300,
                Value = Math.Max(0, _app.StartupDelaySeconds),
                Location = new Point(controlX, y),
                Size = new Size(80, 23)
            };
            var startupDelayHint = new Label
            {
                Text = "Wait time before launching in Start All",
                Location = new Point(controlX + 85, y + 2),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = _normalFont
            };
            this.Controls.Add(startupDelayLabel);
            this.Controls.Add(_startupDelayNumeric);
            this.Controls.Add(startupDelayHint);

            y += rowHeight + 10;

            // --- Crash Recovery & Health Monitoring Section ---
            var settingsSectionLabel = new Label
            {
                Text = "Crash Recovery and Health Monitoring",
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = _boldFont
            };
            this.Controls.Add(settingsSectionLabel);

            y += 22;

            // Hint text for the section
            var sectionHint = new Label
            {
                Text = "When disabled, IMEE will only log and notify issues but not take automatic action.",
                Location = new Point(labelX, y),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = _normalFont,
                MaximumSize = new Size(this.ClientSize.Width - (labelX * 2), 0)
            };
            this.Controls.Add(sectionHint);

            y += sectionHint.PreferredSize.Height + 8;

            // Keep Open
            var keepOpenLabel = new Label
            {
                Text = "Keep Open (Auto-Restart):",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _keepOpenCheckBox = new CheckBox
            {
                Checked = _app.KeepOpen,
                Location = new Point(controlX + 8, y),
                AutoSize = true,
                Text = _app.KeepOpen ? "Yes" : "No"
            };
            this.Controls.Add(keepOpenLabel);
            this.Controls.Add(_keepOpenCheckBox);

            y += rowHeight;

            // Max Retries
            var maxRetriesLabel = new Label
            {
                Text = "Max Retries:",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _maxRetriesNumeric = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 100,
                Value = Math.Max(1, _app.MaxRetries),
                Location = new Point(controlX, y),
                Size = new Size(80, 23)
            };
            this.Controls.Add(maxRetriesLabel);
            this.Controls.Add(_maxRetriesNumeric);

            y += rowHeight;

            // Start Delay
            var startDelayLabel = new Label
            {
                Text = "Restart Delay (sec):",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _startDelayNumeric = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 300,
                Value = Math.Max(1, _app.StartDelaySeconds),
                Location = new Point(controlX, y),
                Size = new Size(80, 23)
            };
            this.Controls.Add(startDelayLabel);
            this.Controls.Add(_startDelayNumeric);

            y += rowHeight;

            // Stable Run Period
            var stableRunLabel = new Label
            {
                Text = "Stable Run Period (sec):",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _stableRunNumeric = new NumericUpDown
            {
                Minimum = 5,
                Maximum = 600,
                Value = Math.Max(5, _app.StableRunPeriodSeconds),
                Location = new Point(controlX, y),
                Size = new Size(80, 23)
            };
            var stableRunHint = new Label
            {
                Text = "Run time before retry count resets",
                Location = new Point(controlX + 85, y + 2),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = _normalFont
            };
            this.Controls.Add(stableRunLabel);
            this.Controls.Add(_stableRunNumeric);
            this.Controls.Add(stableRunHint);

            y += rowHeight;

            // Not Responding Timeout
            var nrtLabel = new Label
            {
                Text = "Not Responding (sec):",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _notRespondingTimeoutNumeric = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 600,
                Value = Math.Max(0, Math.Min(600, _app.NotRespondingTimeoutSeconds)),
                Location = new Point(controlX, y),
                Size = new Size(80, 23)
            };
            var nrtHint = new Label
            {
                Text = "0 = default (2 poll cycles)",
                Location = new Point(controlX + 85, y + 2),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = _normalFont
            };
            this.Controls.Add(nrtLabel);
            this.Controls.Add(_notRespondingTimeoutNumeric);
            this.Controls.Add(nrtHint);

            y += rowHeight;

            // Memory Limit
            var memLabel = new Label
            {
                Text = "Memory Limit (MB):",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _memoryLimitNumeric = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 65536,
                Value = Math.Max(0, Math.Min(65536, _app.MemoryLimitMB)),
                Location = new Point(controlX, y),
                Size = new Size(80, 23)
            };
            var memHint = new Label
            {
                Text = "0 = no limit (disabled)",
                Location = new Point(controlX + 85, y + 2),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = _normalFont
            };
            this.Controls.Add(memLabel);
            this.Controls.Add(_memoryLimitNumeric);
            this.Controls.Add(memHint);

            y += rowHeight;

            // Detect Title Change
            var dtcLabel = new Label
            {
                Text = "Detect Title Change:",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _detectTitleChangeCheckBox = new CheckBox
            {
                Checked = _app.DetectTitleChange,
                Location = new Point(controlX + 8, y),
                AutoSize = true,
                Text = _app.DetectTitleChange ? "Yes" : "No"
            };
            _detectTitleChangeCheckBox.CheckedChanged += (s, e) =>
            {
                _detectTitleChangeCheckBox.Text = _detectTitleChangeCheckBox.Checked ? "Yes" : "No";
            };
            var dtcHint = new Label
            {
                Text = "Kill if window title changes",
                Location = new Point(controlX + 85, y + 2),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = _normalFont
            };
            this.Controls.Add(dtcLabel);
            this.Controls.Add(_detectTitleChangeCheckBox);
            this.Controls.Add(dtcHint);

            y += rowHeight + 10;

            // Set initial enabled state for sub-controls based on Keep Open
            UpdateKeepOpenDependentControls(_keepOpenCheckBox.Checked);

            // Wire up Keep Open checkbox to toggle dependent controls
            _keepOpenCheckBox.CheckedChanged += (s, e) =>
            {
                _keepOpenCheckBox.Text = _keepOpenCheckBox.Checked ? "Yes" : "No";
                UpdateKeepOpenDependentControls(_keepOpenCheckBox.Checked);
            };

            // --- Statistics Section ---
            var statsSectionLabel = new Label
            {
                Text = "Statistics",
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = _boldFont
            };
            this.Controls.Add(statsSectionLabel);

            y += 22;

            // Crash Count
            var crashLabel = new Label
            {
                Text = "Crash Count:",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _crashCountLabel = new Label
            {
                Text = _app.CrashCount.ToString(),
                Location = new Point(controlX, y + 2),
                AutoSize = true,
                Font = _boldFont
            };
            _resetCrashButton = new Button
            {
                Text = "Reset",
                Location = new Point(controlX + 60, y),
                Size = new Size(60, 23),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(243, 156, 18),
                ForeColor = Color.White,
                Font = _smallBoldFont
            };
            _resetCrashButton.Click += (s, e) =>
            {
                _app.CrashCount = 0;
                _crashCountLabel.Text = "0";
            };
            this.Controls.Add(crashLabel);
            this.Controls.Add(_crashCountLabel);
            this.Controls.Add(_resetCrashButton);

            y += rowHeight;

            // Retry Count
            var retryLabel = new Label
            {
                Text = "Retry Count:",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _retryCountLabel = new Label
            {
                Text = $"{_app.RetryCount} / {_app.MaxRetries}",
                Location = new Point(controlX, y + 2),
                AutoSize = true,
                Font = _boldFont
            };
            _resetRetryButton = new Button
            {
                Text = "Reset",
                Location = new Point(controlX + 60, y),
                Size = new Size(60, 23),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(243, 156, 18),
                ForeColor = Color.White,
                Font = _smallBoldFont
            };
            _resetRetryButton.Click += (s, e) =>
            {
                _app.RetryCount = 0;
                _retryCountLabel.Text = $"0 / {(int)_maxRetriesNumeric.Value}";
            };
            this.Controls.Add(retryLabel);
            this.Controls.Add(_retryCountLabel);
            this.Controls.Add(_resetRetryButton);

            y += rowHeight;

            // Last Exit Code
            var exitCodeLabel = new Label
            {
                Text = "Last Exit Code:",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            string exitCodeText = _app.LastExitCode.HasValue ? _app.LastExitCode.Value.ToString() : "N/A";

            _lastExitCodeLabel = new Label
            {
                Text = exitCodeText,
                Location = new Point(controlX, y + 2),
                AutoSize = true,
                Font = _boldFont,
                ForeColor = (_app.LastExitCode.HasValue && _app.LastExitCode.Value != 0) ? Color.Red : Color.Black
            };
            _copyExitCodeButton = new Button
            {
                Text = "Copy",
                Location = new Point(controlX + 100, y),
                Size = new Size(60, 23),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Font = _smallBoldFont,
                Visible = _app.LastExitCode.HasValue
            };
            _copyExitCodeButton.Click += (s, ev) =>
            {
                if (_app.LastExitCode.HasValue)
                {
                    Clipboard.SetText(_app.LastExitCode.Value.ToString());
                }
            };
            this.Controls.Add(exitCodeLabel);
            this.Controls.Add(_lastExitCodeLabel);
            this.Controls.Add(_copyExitCodeButton);

            y += rowHeight + 15;

            // OK / Cancel buttons
            // Place OK/Cancel on the right edge
            int btnWidth = 80;
            int btnHeight = 30;
            int btnSpacing = 10;
            int rightMargin = 20;

            _okButton = new Button
            {
                Text = "OK",
                Location = new Point(this.ClientSize.Width - rightMargin - btnWidth * 2 - btnSpacing, y - 8),
                Size = new Size(btnWidth, btnHeight),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                Font = _buttonFont,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            _okButton.Click += OkButton_Click;

            _cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(this.ClientSize.Width - rightMargin - btnWidth, y - 8),
                Size = new Size(btnWidth, btnHeight),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(192, 57, 43),
                ForeColor = Color.White,
                Font = _buttonFont,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            this.Controls.Add(_okButton);
            this.Controls.Add(_cancelButton);

            this.AcceptButton = _okButton;
            this.CancelButton = _cancelButton;
        }

        /// <summary>
        /// Enables or disables all controls that depend on Keep Open being active.
        /// </summary>
        private void UpdateKeepOpenDependentControls(bool enabled)
        {
            _maxRetriesNumeric.Enabled = enabled;
            _startDelayNumeric.Enabled = enabled;
            _stableRunNumeric.Enabled = enabled;
            _notRespondingTimeoutNumeric.Enabled = enabled;
            _memoryLimitNumeric.Enabled = enabled;
            _detectTitleChangeCheckBox.Enabled = enabled;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_normalFont != null) { _normalFont.Dispose(); _normalFont = null; }
                if (_boldFont != null) { _boldFont.Dispose(); _boldFont = null; }
                if (_smallBoldFont != null) { _smallBoldFont.Dispose(); _smallBoldFont = null; }
                if (_buttonFont != null) { _buttonFont.Dispose(); _buttonFont = null; }
            }
            base.Dispose(disposing);
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*";
                dialog.Title = "Select Application";
                if (!string.IsNullOrEmpty(_directoryTextBox.Text) && File.Exists(_directoryTextBox.Text))
                {
                    dialog.FileName = _directoryTextBox.Text;
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _directoryTextBox.Text = dialog.FileName;
                }
            }
        }

        private void LauncherBrowseButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Launcher Files (*.exe;*.bat;*.cmd;*.vbs;*.ps1)|*.exe;*.bat;*.cmd;*.vbs;*.ps1|Executable Files (*.exe)|*.exe|Batch Files (*.bat;*.cmd)|*.bat;*.cmd|VBScript Files (*.vbs)|*.vbs|PowerShell Scripts (*.ps1)|*.ps1|All Files (*.*)|*.*";
                dialog.Title = "Select Launcher Script or Executable";
                if (!string.IsNullOrEmpty(_launcherTextBox.Text) && File.Exists(_launcherTextBox.Text))
                {
                    dialog.FileName = _launcherTextBox.Text;
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _launcherTextBox.Text = dialog.FileName;
                }
            }
        }

        private void OpenPathButton_Click(object sender, EventArgs e)
        {
            try
            {
                string path = _directoryTextBox.Text.Trim();
                if (string.IsNullOrEmpty(path))
                {
                    MessageBox.Show(this, "Application path is empty.", "Open Path",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (File.Exists(path))
                {
                    Process.Start("explorer.exe", "/select, \"" + path + "\"");
                }
                else
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                    {
                        Process.Start("explorer.exe", "\"" + dir + "\"");
                    }
                    else
                    {
                        MessageBox.Show(this, "The specified path does not exist.", "Open Path",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error opening path: {ex.Message}", "Open Path",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LauncherOpenButton_Click(object sender, EventArgs e)
        {
            try
            {
                string path = _launcherTextBox.Text.Trim();
                if (string.IsNullOrEmpty(path))
                {
                    MessageBox.Show(this, "Launcher path is empty.", "Open Path",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (File.Exists(path))
                {
                    Process.Start("explorer.exe", "/select, \"" + path + "\"");
                }
                else
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                    {
                        Process.Start("explorer.exe", "\"" + dir + "\"");
                    }
                    else
                    {
                        MessageBox.Show(this, "The specified path does not exist.", "Open Path",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error opening path: {ex.Message}", "Open Path",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_directoryTextBox.Text.Trim()))
            {
                MessageBox.Show(this, "Application path cannot be empty.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Validate launcher path if set
            string launcherPath = _launcherTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(launcherPath) && !File.Exists(launcherPath))
            {
                MessageBox.Show(this, "Launcher path does not exist:\n\n" + launcherPath, "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Capture new values
            bool newKeepOpen = _keepOpenCheckBox.Checked;
            bool newHealthMonitoringEnabled = _keepOpenCheckBox.Checked;
            int newMaxRetries = (int)_maxRetriesNumeric.Value;
            int newStartDelaySeconds = (int)_startDelayNumeric.Value;
            int newStartupDelaySeconds = (int)_startupDelayNumeric.Value;
            int newStableRunPeriodSeconds = (int)_stableRunNumeric.Value;
            int newNotRespondingTimeoutSeconds = (int)_notRespondingTimeoutNumeric.Value;
            int newMemoryLimitMB = (int)_memoryLimitNumeric.Value;
            bool newDetectTitleChange = _detectTitleChangeCheckBox.Checked;
            string newLauncherPath = string.IsNullOrEmpty(launcherPath) ? null : launcherPath;

            // Log each individual change for traceability and accountability
            string appName = _app.AppName ?? "(unknown)";
            int changeCount = 0;

            string newDirectory = _directoryTextBox.Text.Trim();
            if (_app.Directory != newDirectory)
            {
                SimpleLogger.Info("AppSettingsChanged @ EditAppDialog.cs",
                    $"'{appName}' Directory changed: \"{Path.GetFileName(_app.Directory)}\" to \"{Path.GetFileName(newDirectory)}\"");
                changeCount++;
            }

            if (_app.KeepOpen != newKeepOpen)
            {
                SimpleLogger.Info("AppSettingsChanged @ EditAppDialog.cs",
                    $"'{appName}' KeepOpen changed: {_app.KeepOpen} to {newKeepOpen}");
                changeCount++;
            }
            if (_app.HealthMonitoringEnabled != newHealthMonitoringEnabled)
            {
                SimpleLogger.Info("AppSettingsChanged @ EditAppDialog.cs",
                    $"'{appName}' HealthMonitoringEnabled changed: {_app.HealthMonitoringEnabled} to {newHealthMonitoringEnabled}");
                changeCount++;
            }
            if (_app.MaxRetries != newMaxRetries)
            {
                SimpleLogger.Info("AppSettingsChanged @ EditAppDialog.cs",
                    $"'{appName}' MaxRetries changed: {_app.MaxRetries} to {newMaxRetries}");
                changeCount++;
            }
            if (_app.StartDelaySeconds != newStartDelaySeconds)
            {
                SimpleLogger.Info("AppSettingsChanged @ EditAppDialog.cs",
                    $"'{appName}' StartDelaySeconds changed: {_app.StartDelaySeconds} to {newStartDelaySeconds}");
                changeCount++;
            }
            if (_app.StartupDelaySeconds != newStartupDelaySeconds)
            {
                SimpleLogger.Info("AppSettingsChanged @ EditAppDialog.cs",
                    $"'{appName}' StartupDelaySeconds changed: {_app.StartupDelaySeconds} to {newStartupDelaySeconds}");
                changeCount++;
            }
            if (_app.StableRunPeriodSeconds != newStableRunPeriodSeconds)
            {
                SimpleLogger.Info("AppSettingsChanged @ EditAppDialog.cs",
                    $"'{appName}' StableRunPeriodSeconds changed: {_app.StableRunPeriodSeconds} to {newStableRunPeriodSeconds}");
                changeCount++;
            }
            if (_app.NotRespondingTimeoutSeconds != newNotRespondingTimeoutSeconds)
            {
                SimpleLogger.Info("AppSettingsChanged @ EditAppDialog.cs",
                    $"'{appName}' NotRespondingTimeoutSeconds changed: {_app.NotRespondingTimeoutSeconds} to {newNotRespondingTimeoutSeconds}");
                changeCount++;
            }
            if (_app.MemoryLimitMB != newMemoryLimitMB)
            {
                SimpleLogger.Info("AppSettingsChanged @ EditAppDialog.cs",
                    $"'{appName}' MemoryLimitMB changed: {_app.MemoryLimitMB} to {newMemoryLimitMB}");
                changeCount++;
            }
            if (_app.DetectTitleChange != newDetectTitleChange)
            {
                SimpleLogger.Info("AppSettingsChanged @ EditAppDialog.cs",
                    $"'{appName}' DetectTitleChange changed: {_app.DetectTitleChange} to {newDetectTitleChange}");
                changeCount++;
            }
            if (_app.LauncherPath != newLauncherPath)
            {
                string oldLauncher = string.IsNullOrEmpty(_app.LauncherPath) ? "(none)" : Path.GetFileName(_app.LauncherPath);
                string newLauncher = string.IsNullOrEmpty(newLauncherPath) ? "(none)" : Path.GetFileName(newLauncherPath);
                SimpleLogger.Info("AppSettingsChanged @ EditAppDialog.cs",
                    $"'{appName}' LauncherPath changed: {oldLauncher} to {newLauncher}");
                changeCount++;
            }
            if (_originalCrashCount != int.Parse(_crashCountLabel.Text))
            {
                SimpleLogger.Info("AppSettingsChanged @ EditAppDialog.cs",
                    $"'{appName}' CrashCount reset: {_originalCrashCount} to {_crashCountLabel.Text}");
                changeCount++;
            }
            if (_originalRetryCount != int.Parse(_retryCountLabel.Text.Split('/')[0].Trim()))
            {
                SimpleLogger.Info("AppSettingsChanged @ EditAppDialog.cs",
                    $"'{appName}' RetryCount reset: {_originalRetryCount} to {_retryCountLabel.Text.Split('/')[0].Trim()}");
                changeCount++;
            }

            if (changeCount == 0)
            {
                SimpleLogger.Info("AppSettingsChanged @ EditAppDialog.cs",
                    $"'{appName}' edit dialog closed with OK — no settings changes detected");
            }
            else
            {
                SimpleLogger.Info("AppSettingsChanged @ EditAppDialog.cs",
                    $"'{appName}' total settings changed: {changeCount}");
            }

            // Apply the new values
            _app.KeepOpen = newKeepOpen;
            _app.HealthMonitoringEnabled = newHealthMonitoringEnabled;
            _app.MaxRetries = newMaxRetries;
            _app.StartDelaySeconds = newStartDelaySeconds;
            _app.StartupDelaySeconds = newStartupDelaySeconds;
            _app.StableRunPeriodSeconds = newStableRunPeriodSeconds;
            _app.NotRespondingTimeoutSeconds = newNotRespondingTimeoutSeconds;
            _app.MemoryLimitMB = newMemoryLimitMB;
            _app.DetectTitleChange = newDetectTitleChange;
            _app.LauncherPath = newLauncherPath;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditAppDialog));
            this.SuspendLayout();
            // 
            // EditAppDialog
            // 
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "EditAppDialog";
            this.ResumeLayout(false);

        }
    }
}
