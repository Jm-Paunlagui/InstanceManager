using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using IntelligentMutexExecutionEnvironment.Models;
using IntelligentMutexExecutionEnvironment.Services;

namespace IntelligentMutexExecutionEnvironment.Utilities
{
    public class SettingsDialog : Form
    {
        private TextBox _stationNameTextBox;
        private CheckBox _runOnStartupCheckBox;

        private NumericUpDown _statusPollIntervalNumeric;
        private NumericUpDown _startGracePeriodNumeric;
        private NumericUpDown _gcCollectIntervalNumeric;
        private NumericUpDown _storageSaveIntervalNumeric;

        private NumericUpDown _logFlushIntervalNumeric;
        private NumericUpDown _logBufferSizeNumeric;
        private NumericUpDown _logRetentionDaysNumeric;

        private ComboBox _profileComboBox;
        private bool _applyingProfile; // guard: prevent ValueChanged feedback loop

        private CheckBox _enforcementEnabledCheckBox;

        private Button _okButton;
        private Button _cancelButton;
        private Button _resetDefaultsButton;
        private Button _serverConfigButton;

        private List<ApplicationGroup> _allGroups;
        private readonly SettingsService _settings;

        private Font _normalFont;
        private Font _boldFont;
        private Font _smallBoldFont;
        private Font _buttonFont;

        // ── Profile presets ──────────────────────────────────────────────────────
        // Order: PollMs, GraceS, GcMin, SaveS, FlushS, BufSize
        // "Custom" has no preset values (null).
        private static readonly string[] ProfileNames =
        {
            "Custom",
            "Very High",
            "High",
            "High-Low",
            "Med-High",
            "Med (Default)",
            "Med-Low",
            "Low-High",
            "Low",
            "Very Low"
        };

        private static readonly int[][] ProfileValues =
        {
            null,                                   // Custom
            new int[] {  2000,  5, 10,  10,  2,  50 },  // Very High
            new int[] {  3000,  7, 15,  15,  5,  75 },  // High
            new int[] {  5000,  8, 20,  20,  7, 100 },  // High-Low
            new int[] {  7000, 10, 25,  25,  8, 100 },  // Med-High
            new int[] { 10000, 10, 30,  30, 10, 100 },  // Med (Default)
            new int[] { 12000, 12, 40,  45, 12, 150 },  // Med-Low
            new int[] { 15000, 15, 50,  60, 15, 200 },  // Low-High
            new int[] { 20000, 20, 60,  90, 20, 300 },  // Low
            new int[] { 30000, 30, 90, 120, 30, 500 },  // Very Low
        };
        // ────────────────────────────────────────────────────────────────────────

        // Public properties for retrieving values after OK
        public string StationName { get { return _stationNameTextBox.Text.Trim(); } }
        public bool RunOnStartup { get { return _runOnStartupCheckBox.Checked; } }
        public int StatusPollIntervalMs { get { return (int)_statusPollIntervalNumeric.Value; } }
        public int StartGracePeriodSeconds { get { return (int)_startGracePeriodNumeric.Value; } }
        public int GcCollectIntervalMinutes { get { return (int)_gcCollectIntervalNumeric.Value; } }
        public int StorageSaveIntervalSeconds { get { return (int)_storageSaveIntervalNumeric.Value; } }
        public int LogFlushIntervalSeconds { get { return (int)_logFlushIntervalNumeric.Value; } }
        public int LogBufferSize { get { return (int)_logBufferSizeNumeric.Value; } }
        public int LogRetentionDays { get { return (int)_logRetentionDaysNumeric.Value; } }
        public bool EnforcementEnabled { get { return _enforcementEnabledCheckBox.Checked; } }

        public SettingsDialog(SettingsService settings, List<ApplicationGroup> allGroups = null)
        {
            _settings = settings;
            _allGroups = allGroups ?? new List<ApplicationGroup>();

            _normalFont = new Font("AUMOVIO Screen", 9F);
            _boldFont = new Font("AUMOVIO Screen", 9F, FontStyle.Bold);
            _smallBoldFont = new Font("AUMOVIO Screen", 7F, FontStyle.Bold);
            _buttonFont = new Font("AUMOVIO Screen", 8F, FontStyle.Bold);

            InitializeControls(settings);
        }

        private void InitializeControls(SettingsService settings)
        {
            this.Text = "Settings";
            this.Size = new Size(520, 615);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = _normalFont;
            this.BackColor = Color.White;

            int labelX = 20;
            int controlX = 210;
            int hintX = controlX + 90;
            int y = 15;
            int rowHeight = 30;

            // ===== General Settings =====
            var generalLabel = new Label
            {
                Text = "General",
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = _boldFont
            };
            this.Controls.Add(generalLabel);
            y += 22;

            // Station Name
            var stationLabel = new Label
            {
                Text = "Station Name:",
                Location = new Point(labelX, y + 3),
                AutoSize = true
            };
            _stationNameTextBox = new TextBox
            {
                Text = settings.StationName,
                Location = new Point(controlX, y),
                Size = new Size(210, 23),
                Font = _normalFont
            };
            this.Controls.Add(stationLabel);
            this.Controls.Add(_stationNameTextBox);
            y += rowHeight;

            // Run on Startup
            _runOnStartupCheckBox = new CheckBox
            {
                Text = "Run on Windows Startup",
                Checked = settings.RunOnStartup,
                Location = new Point(labelX, y + 3),
                AutoSize = true,
                Font = _normalFont
            };
            this.Controls.Add(_runOnStartupCheckBox);
            y += rowHeight - 5;

            // Description label for Run on Startup
            var startupDescLabel = new Label
            {
                Text = "Automatically launch IMEE when Windows starts so managed applications are monitored at all times.",
                Location = new Point(labelX + 17, y),
                Size = new Size(460, 28),
                ForeColor = Color.Gray,
                Font = new Font(_normalFont.FontFamily, 7.5F)
            };
            this.Controls.Add(startupDescLabel);
            y += 30 + 12;

            // ===== Performance Settings =====
            var perfLabel = new Label
            {
                Text = "Performance",
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = _boldFont
            };
            this.Controls.Add(perfLabel);
            y += 22;

            // Performance Profile picker
            var profileLabel = new Label
            {
                Text = "Performance Profile:",
                Location = new Point(labelX, y + 3),
                AutoSize = true
            };
            _profileComboBox = new ComboBox
            {
                Location = new Point(controlX, y),
                Size = new Size(80, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = _normalFont
            };
            _profileComboBox.Items.AddRange(ProfileNames);

            var profileHint = new Label
            {
                Text = "Preset tuning",
                Location = new Point(controlX + 85, y + 3),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = _normalFont
            };

            this.Controls.Add(profileLabel);
            this.Controls.Add(_profileComboBox);
            this.Controls.Add(profileHint);
            y += rowHeight + 4;

            // Status Poll Interval
            AddNumericRow(ref y, labelX, controlX, "Status Poll Interval (ms):",
                1000, 60000, settings.StatusPollIntervalMs, out _statusPollIntervalNumeric,
                "1000 - 60000");

            // Start Grace Period
            AddNumericRow(ref y, labelX, controlX, "Start Grace Period (seconds):",
                1, 120, settings.StartGracePeriodSeconds, out _startGracePeriodNumeric,
                "1 - 120");

            // GC Collect Interval
            AddNumericRow(ref y, labelX, controlX, "GC Collect Interval (minutes):",
                5, 1440, settings.GcCollectIntervalMinutes, out _gcCollectIntervalNumeric,
                "5 - 1440");

            // Storage Save Interval
            AddNumericRow(ref y, labelX, controlX, "Storage Save Interval (seconds):",
                5, 300, settings.StorageSaveIntervalSeconds, out _storageSaveIntervalNumeric,
                "5 - 300");

            y += 12;

            // ===== Logging Settings =====
            var logLabel = new Label
            {
                Text = "Logging",
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = _boldFont
            };
            this.Controls.Add(logLabel);
            y += 22;

            // Log Flush Interval
            AddNumericRow(ref y, labelX, controlX, "Log Flush Interval (seconds):",
                1, 120, settings.LogFlushIntervalSeconds, out _logFlushIntervalNumeric,
                "1 - 120");

            // Log Buffer Size
            AddNumericRow(ref y, labelX, controlX, "Log Buffer Size (entries):",
                10, 1000, settings.LogBufferSize, out _logBufferSizeNumeric,
                "10 - 1000");

            // Log Retention Days
            AddNumericRow(ref y, labelX, controlX, "Log Retention (days):",
                1, 365, settings.LogRetentionDays, out _logRetentionDaysNumeric,
                "1 - 365");

            y += 10;

            // ===== Watchdog Safety =====
            var safetyLabel = new Label
            {
                Text = "Watchdog Safety",
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = _boldFont
            };
            this.Controls.Add(safetyLabel);
            y += 22;

            // Enforcement Enabled
            var enfLabel = new Label
            {
                Text = "Enforcement Enabled:",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _enforcementEnabledCheckBox = new CheckBox
            {
                Checked = settings.EnforcementEnabled,
                Location = new Point(labelX + 148, y),
                AutoSize = true,
                Text = settings.EnforcementEnabled ? "Active" : "Log Only"
            };
            _enforcementEnabledCheckBox.CheckedChanged += (s, ev) =>
            {
                _enforcementEnabledCheckBox.Text = _enforcementEnabledCheckBox.Checked ? "Active" : "Log Only";
            };
            var enfHint = new Label
            {
                Text = "Off = dry-run (log only, no kills)",
                Location = new Point(labelX + 148 + 85, y + 2),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = _normalFont
            };
            this.Controls.Add(enfLabel);
            this.Controls.Add(_enforcementEnabledCheckBox);
            this.Controls.Add(enfHint);
            y += 30;

            y += 10;

            // ===== Buttons =====
            _resetDefaultsButton = new Button
            {
                Text = "Reset Defaults",
                Location = new Point(labelX, y),
                Size = new Size(110, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(243, 156, 18),
                ForeColor = Color.White,
                Font = _smallBoldFont
            };
            _resetDefaultsButton.Click += ResetDefaultsButton_Click;
            this.Controls.Add(_resetDefaultsButton);

            _serverConfigButton = new Button
            {
                Text = "Server Startup...",
                Location = new Point(labelX + 118, y),
                Size = new Size(120, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Font = _smallBoldFont
            };
            _serverConfigButton.Click += ServerConfigButton_Click;
            this.Controls.Add(_serverConfigButton);

            _okButton = new Button
            {
                Text = "OK",
                Location = new Point(310, y),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                Font = _buttonFont
            };
            _okButton.Click += OkButton_Click;
            this.Controls.Add(_okButton);

            _cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(400, y),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("AUMOVIO Screen", 8F)
            };
            this.Controls.Add(_cancelButton);

            this.AcceptButton = _okButton;
            this.CancelButton = _cancelButton;

            // Wire profile events AFTER all controls are created
            _profileComboBox.SelectedIndexChanged += ProfileComboBox_SelectedIndexChanged;
            _statusPollIntervalNumeric.ValueChanged += PerformanceNumeric_ValueChanged;
            _startGracePeriodNumeric.ValueChanged += PerformanceNumeric_ValueChanged;
            _gcCollectIntervalNumeric.ValueChanged += PerformanceNumeric_ValueChanged;
            _storageSaveIntervalNumeric.ValueChanged += PerformanceNumeric_ValueChanged;
            _logFlushIntervalNumeric.ValueChanged += PerformanceNumeric_ValueChanged;
            _logBufferSizeNumeric.ValueChanged += PerformanceNumeric_ValueChanged;

            // Select the profile that matches the current settings (or "Custom")
            _profileComboBox.SelectedIndex = DetectCurrentProfile();
        }

        /// <summary>
        /// Returns the profile index whose values match the current numeric settings,
        /// or 0 (Custom) if no profile matches.
        /// </summary>
        private int DetectCurrentProfile()
        {
            int poll = (int)_statusPollIntervalNumeric.Value;
            int grace = (int)_startGracePeriodNumeric.Value;
            int gc = (int)_gcCollectIntervalNumeric.Value;
            int save = (int)_storageSaveIntervalNumeric.Value;
            int flush = (int)_logFlushIntervalNumeric.Value;
            int buf = (int)_logBufferSizeNumeric.Value;

            for (int i = 1; i < ProfileValues.Length; i++)
            {
                int[] p = ProfileValues[i];
                if (p[0] == poll && p[1] == grace && p[2] == gc &&
                    p[3] == save && p[4] == flush && p[5] == buf)
                    return i;
            }
            return 0; // Custom
        }

        private void ProfileComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = _profileComboBox.SelectedIndex;
            if (idx <= 0 || ProfileValues[idx] == null)
                return; // Custom selected — leave numerics as-is

            _applyingProfile = true;
            try
            {
                int[] p = ProfileValues[idx];
                _statusPollIntervalNumeric.Value = p[0];
                _startGracePeriodNumeric.Value = p[1];
                _gcCollectIntervalNumeric.Value = p[2];
                _storageSaveIntervalNumeric.Value = p[3];
                _logFlushIntervalNumeric.Value = p[4];
                _logBufferSizeNumeric.Value = p[5];
            }
            finally
            {
                _applyingProfile = false;
            }
        }

        private void PerformanceNumeric_ValueChanged(object sender, EventArgs e)
        {
            // If a profile is being applied, don't interfere
            if (_applyingProfile) return;

            // Switch dropdown to "Custom" if values no longer match any profile
            int matched = DetectCurrentProfile();
            if (_profileComboBox.SelectedIndex != matched)
                _profileComboBox.SelectedIndex = matched;
        }

        private void AddNumericRow(ref int y, int labelX, int controlX, string labelText,
            int min, int max, int value, out NumericUpDown numeric, string hint)
        {
            var label = new Label
            {
                Text = labelText,
                Location = new Point(labelX, y + 3),
                AutoSize = true
            };
            numeric = new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Value = Math.Max(min, Math.Min(max, value)),
                Location = new Point(controlX, y),
                Size = new Size(80, 23)
            };
            var hintLabel = new Label
            {
                Text = hint,
                Location = new Point(controlX + 85, y + 3),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = _normalFont
            };
            this.Controls.Add(label);
            this.Controls.Add(numeric);
            this.Controls.Add(hintLabel);
            y += 30;
        }

        private void ResetDefaultsButton_Click(object sender, EventArgs e)
        {
            _statusPollIntervalNumeric.Value = SettingsService.DefaultStatusPollIntervalMs;
            _startGracePeriodNumeric.Value = SettingsService.DefaultStartGracePeriodSeconds;
            _gcCollectIntervalNumeric.Value = SettingsService.DefaultGcCollectIntervalMinutes;
            _storageSaveIntervalNumeric.Value = SettingsService.DefaultStorageSaveIntervalSeconds;
            _logFlushIntervalNumeric.Value = SettingsService.DefaultLogFlushIntervalSeconds;
            _logBufferSizeNumeric.Value = SettingsService.DefaultLogBufferSize;
            _logRetentionDaysNumeric.Value = SettingsService.DefaultLogRetentionDays;
            _enforcementEnabledCheckBox.Checked = SettingsService.DefaultEnforcementEnabled;
            // Station name is intentionally NOT reset
        }

        private void ServerConfigButton_Click(object sender, EventArgs e)
        {
            using (var dlg = new ServerConfigDialog(
                _allGroups,
                _settings.IsServerMode,
                _settings.ServerAutoRunMode,
                _settings.ServerAutoRunGroupIdList))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _settings.SaveServerSettings(dlg.IsServerMode, dlg.ServerAutoRunMode, dlg.ServerAutoRunGroupIds);
                }
            }
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
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

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsDialog));
            this.SuspendLayout();
            //
            // SettingsDialog
            //
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "SettingsDialog";
            this.ResumeLayout(false);

        }
    }
}
