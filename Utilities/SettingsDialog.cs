using System;
using System.Drawing;
using System.Windows.Forms;
using IntelligentMutexExecutionEnvironment.Services;

namespace IntelligentMutexExecutionEnvironment.Utilities
{
    public class SettingsDialog : Form
    {
        private TextBox _stationNameTextBox;

        private NumericUpDown _statusPollIntervalNumeric;
        private NumericUpDown _startGracePeriodNumeric;
        private NumericUpDown _gcCollectIntervalNumeric;
        private NumericUpDown _storageSaveIntervalNumeric;

        private NumericUpDown _logFlushIntervalNumeric;
        private NumericUpDown _logBufferSizeNumeric;
        private NumericUpDown _logRetentionDaysNumeric;

        private Button _okButton;
        private Button _cancelButton;
        private Button _resetDefaultsButton;

        private Font _normalFont;
        private Font _boldFont;
        private Font _smallBoldFont;
        private Font _buttonFont;

        // Public properties for retrieving values after OK
        public string StationName { get { return _stationNameTextBox.Text.Trim(); } }
        public int StatusPollIntervalMs { get { return (int)_statusPollIntervalNumeric.Value; } }
        public int StartGracePeriodSeconds { get { return (int)_startGracePeriodNumeric.Value; } }
        public int GcCollectIntervalMinutes { get { return (int)_gcCollectIntervalNumeric.Value; } }
        public int StorageSaveIntervalSeconds { get { return (int)_storageSaveIntervalNumeric.Value; } }
        public int LogFlushIntervalSeconds { get { return (int)_logFlushIntervalNumeric.Value; } }
        public int LogBufferSize { get { return (int)_logBufferSizeNumeric.Value; } }
        public int LogRetentionDays { get { return (int)_logRetentionDaysNumeric.Value; } }

        public SettingsDialog(SettingsService settings)
        {
            _normalFont = new Font("AUMOVIO Screen", 9F);
            _boldFont = new Font("AUMOVIO Screen", 9F, FontStyle.Bold);
            _smallBoldFont = new Font("AUMOVIO Screen", 7F, FontStyle.Bold);
            _buttonFont = new Font("AUMOVIO Screen", 8F, FontStyle.Bold);

            InitializeControls(settings);
        }

        private void InitializeControls(SettingsService settings)
        {
            this.Text = "Settings";
            this.Size = new Size(520, 530);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = _normalFont;
            this.BackColor = Color.White;

            int labelX = 20;
            int controlX = 270;
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
            y += rowHeight + 12;

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

            y += 20;

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
            // Station name is intentionally NOT reset
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
    }
}
